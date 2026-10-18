using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Payments.Services;

namespace modaar.api.Features.Payments.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _payments;

    public PaymentsController(IPaymentService payments) => _payments = payments;

    // Newest first. Scoped by account type: a tenant sees what they paid, an owner and broker
    // what was paid on their properties.
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<PaymentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] PaymentListQueryDto query, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } activeRole)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
        return (await _payments.ListAsync(userId, query,activeRole, ct)).ToActionResult();
    }

    // The "paid this year" and "remaining on the contract" cards. Same scope as the list.
    [HttpGet("summary")]
    [ProducesResponseType(typeof(PaymentSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } activeRole)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

        return (await _payments.GetSummaryAsync(userId, activeRole, ct)).ToActionResult();
    }

    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType(typeof(InvoiceDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(Guid paymentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } activeRole)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

        return (await _payments.GetInvoiceAsync(paymentId, userId, activeRole, ct)).ToActionResult();
    }

    // Redirects to the stored document. 404 until one has been uploaded — no generator exists.
    [HttpGet("{paymentId:guid}/invoice/download")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadInvoice(Guid paymentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } activeRole)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

        var result = await _payments.GetInvoiceUrlAsync(paymentId, userId, activeRole, ct);
        return result.Success ? Redirect(result.Value!) : result.ToActionResult();
    }
}
