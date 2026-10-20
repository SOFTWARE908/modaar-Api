using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Payments.Gateway;
using modaar.api.Features.Payments.Services;

namespace modaar.api.Features.Payments.Controllers;

// Paying an owed installment: by card (mada / Visa / Mastercard through Moyasar) or by bank
// transfer with a receipt the owner or broker reviews. See PayService for the full flow.
[ApiController]
[Route("api/payments")]
[Authorize]
[Produces("application/json")]
public class PayController : ControllerBase
{
    private readonly IPayService _pay;

    public PayController(IPayService pay) => _pay = pay;

    // Tenant. Starts a card payment: returns the amount and the metadata to hand to the SDK.
    [HttpPost("{paymentId:guid}/card/checkout")]
    [ProducesResponseType(typeof(CardCheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CardCheckout(Guid paymentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _pay.StartCardCheckoutAsync(paymentId, userId, ct)).ToActionResult();
    }

    // Tenant. After the SDK finishes: the server checks the charge with Moyasar and settles it.
    // status "paid" or "failed" in the body; 409 while the charge isn't finished (3-D Secure).
    [HttpPost("{paymentId:guid}/card/confirm")]
    [ProducesResponseType(typeof(PaymentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CardConfirm(Guid paymentId, [FromBody] ConfirmCardPaymentDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _pay.ConfirmCardAsync(paymentId, userId, request, ct)).ToActionResult();
    }

    // The platform account to transfer to.
    [HttpGet("bank-transfer-info")]
    [ProducesResponseType(typeof(BankTransferInfoDto), StatusCodes.Status200OK)]
    public IActionResult BankTransferInfo() => Ok(_pay.GetBankTransferInfo());

    // Tenant. multipart/form-data with one "file" field (image or PDF, up to 10 MB). The payment
    // becomes underReview until the owner or broker decides.
    [HttpPost("{paymentId:guid}/bank-transfer")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [ProducesResponseType(typeof(BankTransferDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitBankTransfer(Guid paymentId, IFormFile file, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (file is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No file was supplied.");

        return (await _pay.SubmitBankTransferAsync(paymentId, userId, file, ct)).ToActionResult();
    }

    // Owner or the property's broker: "إرسال تذكير بالسداد" to the tenant. Unpaid installments
    // only; once per 24 hours per installment (409 with the next time otherwise).
    [HttpPost("{paymentId:guid}/remind")]
    [ProducesResponseType(typeof(PaymentReminderResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remind(Guid paymentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _pay.SendReminderAsync(paymentId, userId, ct)).ToActionResult();
    }

    // Owner or broker running the property: the money arrived.
    [HttpPost("{paymentId:guid}/bank-transfer/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveBankTransfer(Guid paymentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _pay.ApproveBankTransferAsync(paymentId, userId, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    // Owner or broker running the property: the receipt doesn't check out. Owed again.
    [HttpPost("{paymentId:guid}/bank-transfer/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectBankTransfer(Guid paymentId, [FromBody] RejectBankTransferDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _pay.RejectBankTransferAsync(paymentId, userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }
}

// Moyasar calls this when a payment changes. Set the URL and a secret token in the Moyasar
// dashboard, and the same token as Payments:Moyasar:WebhookSecret.
[ApiController]
[Route("api/webhooks/moyasar")]
[AllowAnonymous]
public class MoyasarWebhookController : ControllerBase
{
    private readonly IPayService _pay;
    private readonly PaymentSettings _settings;

    public MoyasarWebhookController(IPayService pay, IOptions<PaymentSettings> settings)
    {
        _pay = pay;
        _settings = settings.Value;
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] JsonElement body, CancellationToken ct)
    {
        var secret = _settings.Moyasar.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
            return NotFound();

        var token = body.TryGetProperty("secret_token", out var t) ? t.GetString() : null;
        if (!string.Equals(token, secret, StringComparison.Ordinal))
            return Unauthorized();

        // Only the payment id is taken from the body; the payment itself is read back from Moyasar.
        if (body.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id)
            && id.GetString() is { Length: > 0 } gatewayPaymentId)
            await _pay.HandleGatewayNotificationAsync(gatewayPaymentId, ct);

        // 200 even for events we ignore, so Moyasar doesn't keep retrying them.
        return Ok();
    }
}
