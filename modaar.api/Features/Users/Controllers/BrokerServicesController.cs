using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Services;

namespace modaar.api.Features.Users.Controllers;

// What brokerages offer: one admin-managed list, for the marketplace filter and the broker's
// profile picker.
[ApiController]
[Route("api/broker-services")]
[Authorize]
[Produces("application/json")]
public class BrokerServicesController : ControllerBase
{
    private readonly IBrokerServiceCategoryService _services;

    public BrokerServicesController(IBrokerServiceCategoryService services) => _services = services;

    // Active ones. Any signed-in user.
    [HttpGet]
    [ProducesResponseType(typeof(List<BrokerServiceCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _services.ListAsync(ct));

    // Admin: every one, retired included.
    [HttpGet("admin")]
    [ProducesResponseType(typeof(List<BrokerServiceCategoryAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListForAdmin(CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _services.ListForAdminAsync(c.Role, ct)).ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(BrokerServiceCategoryAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateBrokerServiceCategoryDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var result = await _services.CreateAsync(c.Role, request, ct);
        return result.Success ? CreatedAtAction(nameof(ListForAdmin), null, result.Value) : result.ToActionResult();
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(BrokerServiceCategoryAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBrokerServiceCategoryDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _services.UpdateAsync(id, c.Role, request, ct)).ToActionResult();
    }

    // Deactivates.
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var result = await _services.DeleteAsync(id, c.Role, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    private (Guid UserId, AccountType Role)? Caller() =>
        User.GetUserId() is { } id && User.GetActiveRole() is { } role ? (id, role) : null;

    private IActionResult NoCaller() =>
        User.GetUserId() is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.")
            : Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
}
