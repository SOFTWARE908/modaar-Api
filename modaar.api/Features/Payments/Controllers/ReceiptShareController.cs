using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Payments.Services;

namespace modaar.api.Features.Payments.Controllers;

// "مشاركة الإيصال": make a public link (signed in), then anyone with it opens the receipt page.
[ApiController]
public class ReceiptShareController : ControllerBase
{
    private readonly IReceiptShareService _share;

    public ReceiptShareController(IReceiptShareService share) => _share = share;

    // Tenant, owner or broker of the payment; paid payments only. Returns the same link while it
    // is still valid (30 days).
    [HttpPost("api/payments/{paymentId:guid}/share-link")]
    [Authorize]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ReceiptShareLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(Guid paymentId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } role)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return (await _share.CreateAsync(paymentId, userId, role, baseUrl, ct)).ToActionResult();
    }

    // The public page. No sign-in: the token in the link is the permission.
    [HttpGet("r/{token}")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Page(string token, CancellationToken ct)
    {
        // Tokens are 43 URL-safe characters; anything else can't be one, so skip the lookup.
        if (token.Length != 43 || token.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            return NotFoundPage();

        var html = await _share.RenderAsync(token, ct);
        return html is null ? NotFoundPage() : Content(html, "text/html; charset=utf-8");
    }

    private ContentResult NotFoundPage() => new()
    {
        StatusCode = StatusCodes.Status404NotFound,
        ContentType = "text/html; charset=utf-8",
        Content = """
            <!doctype html><html lang="ar" dir="rtl"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1"><meta name="robots" content="noindex">
            <title>الرابط غير صالح</title></head>
            <body style="font-family:system-ui,Tahoma,sans-serif;text-align:center;padding:48px 16px;color:#1d2a33">
            <h1 style="font-size:18px">الرابط غير صالح أو انتهت صلاحيته</h1></body></html>
            """
    };
}
