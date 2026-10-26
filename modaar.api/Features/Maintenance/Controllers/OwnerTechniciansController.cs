using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Services;

namespace modaar.api.Features.Maintenance.Controllers;

// Admin: an owner's private technicians — the ones the owner picks from on units they run
// themselves. The owner reads them through GET /api/technicians.
[ApiController]
[Route("api/admin/owners/{ownerUserId:guid}/technicians")]
[Authorize]
[Produces("application/json")]
public class OwnerTechniciansController : ControllerBase
{
    private readonly IOwnerTechnicianService _technicians;

    public OwnerTechniciansController(IOwnerTechnicianService technicians) => _technicians = technicians;

    [HttpGet]
    [ProducesResponseType(typeof(List<OwnerTechnicianDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(Guid ownerUserId, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _technicians.ListAsync(ownerUserId, c.Role, ct)).ToActionResult();
    }

    // By phone: reuses an existing technician (or account) with that number, else creates one.
    [HttpPost]
    [ProducesResponseType(typeof(OwnerTechnicianDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Add(Guid ownerUserId, [FromBody] AddOwnerTechnicianDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _technicians.AddAsync(ownerUserId, c.UserId, c.Role, request, ct)).ToActionResult();
    }

    // Unlinks only; the technician stays (other owners, past requests).
    [HttpDelete("{technicianId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid ownerUserId, Guid technicianId, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var result = await _technicians.RemoveAsync(ownerUserId, technicianId, c.Role, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    private (Guid UserId, Features.Authentication.Enums.AccountType Role)? Caller() =>
        User.GetUserId() is { } id && User.GetActiveRole() is { } role ? (id, role) : null;

    private IActionResult NoCaller() =>
        User.GetUserId() is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.")
            : Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
}
