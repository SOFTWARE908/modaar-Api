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

        return (await _payments.ListAsync(userId, query, ct)).ToActionResult();
    }

    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType(typeof(InvoiceDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(Guid paymentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _payments.GetInvoiceAsync(paymentId, userId, ct)).ToActionResult();
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

        var result = await _payments.GetInvoiceUrlAsync(paymentId, userId, ct);
        return result.Success ? Redirect(result.Value!) : result.ToActionResult();
    }
}
