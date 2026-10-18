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

    // The "what did you like?" chips, with both labels so the app can switch language offline.
    [HttpGet("~/api/maintenance-requests/rating-tags")]
    [ProducesResponseType(typeof(IReadOnlyList<MaintenanceRatingTagDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RatingTags(CancellationToken ct) =>
        (await _feedback.ListRatingTagsAsync(ct)).ToActionResult();

    // Available once the request is Completed and a technician was assigned. POST and PUT do the
    // same thing — create or replace the one rating — so "edit rating" can use either.
    [HttpPost("rating")]
    [HttpPut("rating")]
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
