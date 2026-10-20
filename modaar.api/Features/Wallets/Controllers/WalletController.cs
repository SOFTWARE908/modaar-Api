using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Wallets.Dtos;
using modaar.api.Features.Wallets.Services;

namespace modaar.api.Features.Wallets.Controllers;

// The owner's wallet: balance, history, and card top-up. Owner role only.
[ApiController]
[Route("api/wallet")]
[Authorize]
[Produces("application/json")]
public class WalletController : ControllerBase
{
    private readonly IWalletService _wallet;

    public WalletController(IWalletService wallet) => _wallet = wallet;

    [HttpGet]
    [ProducesResponseType(typeof(WalletDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (Owner() is not { } userId) return OwnerOnly();
        return (await _wallet.GetAsync(userId, ct)).ToActionResult();
    }

    // Newest first.
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(PagedResultDto<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Transactions([FromQuery] PagedRequestDto query, CancellationToken ct)
    {
        if (Owner() is not { } userId) return OwnerOnly();
        return (await _wallet.ListTransactionsAsync(userId, query, ct)).ToActionResult();
    }

    // Same shape as an installment's card checkout; hand it to the Moyasar SDK.
    [HttpPost("top-up/checkout")]
    [ProducesResponseType(typeof(CardCheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> StartTopUp([FromBody] StartTopUpDto request, CancellationToken ct)
    {
        if (Owner() is not { } userId) return OwnerOnly();
        return (await _wallet.StartTopUpAsync(userId, request, ct)).ToActionResult();
    }

    // After the SDK: the server checks the charge with Moyasar and credits the wallet.
    [HttpPost("top-up/confirm")]
    [ProducesResponseType(typeof(WalletTopUpResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmTopUp([FromBody] ConfirmCardPaymentDto request, CancellationToken ct)
    {
        if (Owner() is not { } userId) return OwnerOnly();
        return (await _wallet.ConfirmTopUpAsync(userId, request, ct)).ToActionResult();
    }

    private Guid? Owner() =>
        User.GetUserId() is { } id && User.GetActiveRole() == AccountType.Owner ? id : null;

    private IActionResult OwnerOnly() =>
        User.GetUserId() is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.")
            : Problem(statusCode: StatusCodes.Status403Forbidden, detail: "The wallet is for owners. Select the owner role first.");
}
