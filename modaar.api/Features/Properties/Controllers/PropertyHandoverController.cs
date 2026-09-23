using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Properties.Dtos;
using modaar.api.Features.Properties.Services;

namespace modaar.api.Features.Properties.Controllers;

// Split from PropertiesController: handover is its own record with its own permissions, and the
// property controller is already the list-and-details surface.
[ApiController]
[Route("api/properties/{propertyId:guid}/handover")]
[Authorize]
[Produces("application/json")]
public class PropertyHandoverController : ControllerBase
{
    private readonly IPropertyHandoverService _handover;

    public PropertyHandoverController(IPropertyHandoverService handover) => _handover = handover;

    // Owner, managing broker, or the tenant of the current tenancy.
    [HttpGet]
    [ProducesResponseType(typeof(PropertyHandoverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid propertyId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _handover.GetAsync(propertyId, userId, ct)).ToActionResult();
    }

    // Owner or managing broker. Sending a signature completes it; without one it stays a draft
    // that can be resubmitted.
    [HttpPost]
    [ProducesResponseType(typeof(PropertyHandoverDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Submit(
        Guid propertyId, [FromBody] SubmitPropertyHandoverDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _handover.SubmitAsync(propertyId, userId, request, ct)).ToActionResult();
    }
}
