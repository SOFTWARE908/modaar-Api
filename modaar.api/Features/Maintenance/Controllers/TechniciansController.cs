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

        return (await _technicians.ListAsync(userId, serviceTypeId, ct)).ToActionResult();
    }
}
