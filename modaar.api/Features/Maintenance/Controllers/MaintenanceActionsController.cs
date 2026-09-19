using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Services;

namespace modaar.api.Features.Maintenance.Controllers;

// The owner and broker side of a request: deciding what happens to it. Split from
// MaintenanceRequestsController, which is the tenant-facing lifecycle.
//
// All four are pending-only. Accept / reject / inquiry: owner or managing broker.
// Delegate: owner alone.
[ApiController]
[Route("api/maintenance-requests/{requestId:guid}")]
[Authorize]
[Produces("application/json")]
public class MaintenanceActionsController : ControllerBase
{
    private readonly IMaintenanceActionService _actions;

    public MaintenanceActionsController(IMaintenanceActionService actions) => _actions = actions;

    [HttpPost("accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept(
        Guid requestId, [FromBody] AcceptMaintenanceRequestDto? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        // The body is optional: accepting with nothing in it agrees to the tenant's requested slot.
        var result = await _actions.AcceptAsync(
            requestId, userId, request ?? new AcceptMaintenanceRequestDto(), ct);

        return result.Success ? NoContent() : result.ToActionResult();
    }

    [HttpPost("reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid requestId, [FromBody] RejectMaintenanceRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.RejectAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // Asks the tenant a question. The request stays Pending and can still be accepted or rejected.
    [HttpPost("inquiry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inquiry(
        Guid requestId, [FromBody] MaintenanceInquiryDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.InquiryAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // Owner only — hands the job to the brokerage already managing the property.
    [HttpPost("delegate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delegate(
        Guid requestId, [FromBody] DelegateMaintenanceRequestDto? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.DelegateAsync(
            requestId, userId, request ?? new DelegateMaintenanceRequestDto(), ct);

        return result.Success ? NoContent() : result.ToActionResult();
    }
}
