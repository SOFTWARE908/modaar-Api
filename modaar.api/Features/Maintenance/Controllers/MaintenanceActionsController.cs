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
// Who may call what, and when, follows the owner's subscription plan; see MaintenanceStages.
// The details response lists the caller's availableActions. 403 = someone else's action,
// 409 = not possible at this stage.
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

    // The handler prices the work once accepted. Resubmitting replaces the quote and, on Managed,
    // asks the owner again (unless the new total is under their auto-approve limit).
    [HttpPut("quote")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitQuote(
        Guid requestId, [FromBody] SubmitMaintenanceQuoteDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.SubmitQuoteAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // Owner, Managed plan: approves the broker's price so a technician can be sent.
    [HttpPost("quote/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveQuote(
        Guid requestId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.ApproveQuoteAsync(requestId, userId, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // Owner, Managed plan: turns the price down, which closes the request. To negotiate, use inquiry.
    [HttpPost("quote/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectQuote(
        Guid requestId, [FromBody] RejectMaintenanceRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.RejectQuoteAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // The handler sends a technician at a set date and time. Calling it again replaces both.
    [HttpPost("assign-technician")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignTechnician(
        Guid requestId, [FromBody] AssignTechnicianDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.AssignTechnicianAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // "تغيير الموعد": same technician, new date and time.
    [HttpPost("reschedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(
        Guid requestId, [FromBody] RescheduleMaintenanceVisitDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.RescheduleAsync(requestId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // "تأكيد اكتمال العمل". Opens rating for the tenant.
    [HttpPost("complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(
        Guid requestId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _actions.CompleteAsync(requestId, userId, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }
}
