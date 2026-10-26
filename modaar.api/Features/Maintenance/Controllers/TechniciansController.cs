using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Services;

namespace modaar.api.Features.Maintenance.Controllers;

[ApiController]
[Route("api/technicians")]
[Authorize]
[Produces("application/json")]
public class TechniciansController : ControllerBase
{
    private readonly ITechnicianService _technicians;

    public TechniciansController(ITechnicianService technicians) => _technicians = technicians;

    // The technicians the caller can assign, best rated first. serviceTypeId narrows it to the
    // request's category.
    [HttpGet]
    [ProducesResponseType(typeof(List<TechnicianListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] Guid? serviceTypeId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _technicians.ListAsync(userId, User.GetActiveRole(), serviceTypeId, ct)).ToActionResult();
    }

    // The technician's week. Days not listed are days off.
    [HttpGet("{technicianId:guid}/working-hours")]
    [ProducesResponseType(typeof(List<WorkingHoursDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkingHours(Guid technicianId, CancellationToken ct) =>
        (await _technicians.GetWorkingHoursAsync(technicianId, ct)).ToActionResult();

    // Replaces the whole week. The technician themselves, or the brokerage they work for.
    [HttpPut("{technicianId:guid}/working-hours")]
    [ProducesResponseType(typeof(List<WorkingHoursDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetWorkingHours(Guid technicianId, [FromBody] SetWorkingHoursDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _technicians.SetWorkingHoursAsync(technicianId, userId, request, ct)).ToActionResult();
    }

    // Free two-hour slots, by day. ?from=2026-10-26 (default today) &days=1..14 (default 7).
    [HttpGet("{technicianId:guid}/availability")]
    [ProducesResponseType(typeof(TechnicianAvailabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Availability(Guid technicianId, [FromQuery] DateOnly? from, [FromQuery] int days = 7, CancellationToken ct = default) =>
        (await _technicians.GetAvailabilityAsync(technicianId, from, days, ct)).ToActionResult();
}
