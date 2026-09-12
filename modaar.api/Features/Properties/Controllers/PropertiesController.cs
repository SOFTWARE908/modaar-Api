using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Properties.Dtos;
using modaar.api.Features.Properties.Services;

namespace modaar.api.Features.Properties.Controllers;

[ApiController]
[Route("api/properties")]
[Authorize]
[Produces("application/json")]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyService _properties;

    public PropertiesController(IPropertyService properties) => _properties = properties;

    // The owner's portfolio. A tenant or broker calling this gets an empty page rather than a
    // 403: they own nothing, which is a true answer and not an error.
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<PropertyListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] PropertyListQueryDto query, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _properties.ListForOwnerAsync(userId, query, ct)).ToActionResult();
    }

    [HttpGet("{propertyId:guid}")]
    [ProducesResponseType(typeof(PropertyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid propertyId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _properties.GetDetailsAsync(propertyId, userId, ct)).ToActionResult();
    }




    [HttpPost]
    [ProducesResponseType(typeof(PropertyDetailsDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _properties.CreateAsync(userId, request, ct);
        if (!result.Success)
            return result.ToActionResult();

        return CreatedAtAction(nameof(Get), new { propertyId = result.Value!.Summary.Id }, result.Value);
    }

    // Partial update: omitted fields are left alone. Sending Images replaces the whole gallery.
    [HttpPatch("{propertyId:guid}")]
    [ProducesResponseType(typeof(PropertyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid propertyId, [FromBody] UpdatePropertyRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _properties.UpdateAsync(propertyId, userId, request, ct)).ToActionResult();
    }

    // Soft delete. Refused with 409 while a contract is active on the unit.
    [HttpDelete("{propertyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid propertyId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _properties.DeleteAsync(propertyId, userId, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }
}
