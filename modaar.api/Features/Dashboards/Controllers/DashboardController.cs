using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Dashboards.Dtos;
using modaar.api.Features.Dashboards.Services;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Dashboards.Controllers;

// Three paths rather than one that switches on account type, matching the spec. Each refuses a
// caller whose account type doesn't match, so a tenant cannot read the owner aggregate.
[ApiController]
[Route("api/dashboard")]
[Authorize]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboards;

    public DashboardController(IDashboardService dashboards) => _dashboards = dashboards;

    [HttpGet("owner")]
    [ProducesResponseType(typeof(OwnerDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Owner(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _dashboards.GetOwnerAsync(userId, ct)).ToActionResult();
    }

    [HttpGet("tenant")]
    [ProducesResponseType(typeof(TenantDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Tenant(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _dashboards.GetTenantAsync(userId, ct)).ToActionResult();
    }

    [HttpGet("broker")]
    [ProducesResponseType(typeof(BrokerDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Broker(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _dashboards.GetBrokerAsync(userId, ct)).ToActionResult();
    }
}
