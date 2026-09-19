using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Services;

namespace modaar.api.Features.Maintenance.Controllers;

// Tenant-facing feedback on a finished or troubled visit. Split from MaintenanceRequestsController
// because the audience is different: that one is the request lifecycle, shared by tenants, owners
// and brokers; everything here is the tenant alone, after the fact.
[ApiController]
[Route("api/maintenance-requests/{requestId:guid}")]
[Authorize]
[Produces("application/json")]
public class MaintenanceFeedbackController : ControllerBase
{
    private readonly IMaintenanceFeedbackService _feedback;

    public MaintenanceFeedbackController(IMaintenanceFeedbackService feedback) => _feedback = feedback;

    // Available once the request is Completed and a technician was assigned. Resubmitting edits
    // the existing rating rather than failing.
    [HttpPost("rating")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitRating(
        Guid requestId, [FromBody] MaintenanceRatingSubmitDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _feedback.SubmitRatingAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // One open ticket at a time per request.
    [HttpPost("need-help")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> NeedHelp(
        Guid requestId, [FromBody] NeedHelpRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _feedback.SubmitNeedHelpAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }
}
