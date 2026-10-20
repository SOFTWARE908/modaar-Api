using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Payments.Gateway;
using modaar.api.Features.Wallets.Dtos;
using modaar.api.Features.Wallets.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Wallets.Services;

public interface IWalletService
{
    Task<Result<WalletDto>> GetAsync(Guid ownerUserId, CancellationToken ct);
    Task<Result<PagedResultDto<WalletTransactionDto>>> ListTransactionsAsync(Guid ownerUserId, PagedRequestDto query, CancellationToken ct);
    Task<Result<CardCheckoutDto>> StartTopUpAsync(Guid ownerUserId, StartTopUpDto request, CancellationToken ct);
    Task<Result<WalletTopUpResultDto>> ConfirmTopUpAsync(Guid ownerUserId, ConfirmCardPaymentDto request, CancellationToken ct);

    // From the Moyasar webhook, for a charge tagged as a wallet top-up.
    Task SettleFromGatewayAsync(GatewayPayment charge, CancellationToken ct);
}

// The owner's wallet. For now money only comes in, by card top-up, through the same Moyasar flow
// as paying an installment: checkout → the app charges the card with the SDK → confirm, where the
// server reads the charge back from Moyasar and checks it before crediting.
//
// Callers are already known to be owners (the controller checks the active role).
public sealed class WalletService : IWalletService
{
    private readonly ModaarDbContext _db;
    private readonly IPaymentGatewayClient _gateway;
    private readonly PaymentSettings _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        ModaarDbContext db, IPaymentGatewayClient gateway, IOptions<PaymentSettings> settings,
        TimeProvider time, ILogger<WalletService> logger)
    {
        _db = db;
        _gateway = gateway;
        _settings = settings.Value;
        _time = time;
        _logger = logger;
    }

    public async Task<Result<WalletDto>> GetAsync(Guid ownerUserId, CancellationToken ct)
    {
        var wallet = await GetOrCreateAsync(ownerUserId, ct);
        return Result<WalletDto>.Ok(new WalletDto { Balance = wallet.Balance, Currency = wallet.Currency });
    }

    public async Task<Result<PagedResultDto<WalletTransactionDto>>> ListTransactionsAsync(
        Guid ownerUserId, PagedRequestDto query, CancellationToken ct)
    {
        var wallet = await GetOrCreateAsync(ownerUserId, ct);
        var rows = _db.WalletTransactions.AsNoTracking().Where(t => t.WalletId == wallet.Id);

        var total = await rows.CountAsync(ct);
        var items = await rows
            .OrderByDescending(t => t.CreatedAt)
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .ToListAsync(ct);

        return Result<PagedResultDto<WalletTransactionDto>>.Ok(new PagedResultDto<WalletTransactionDto>
        {
            TotalCount = total,
            Items = items.Select(t => new WalletTransactionDto
            {
                Id = t.Id,
                Type = t.Type,
                Status = t.Status,
                Amount = t.Amount,
                Currency = t.Currency,
                BalanceAfter = t.BalanceAfter,
                PaymentMethod = CardLabels.Display(t.CardBrand, t.CardLast4),
                FailureReason = t.FailureReason,
                CreatedAt = t.CreatedAt
            }).ToList()
        });
    }

    public async Task<Result<CardCheckoutDto>> StartTopUpAsync(Guid ownerUserId, StartTopUpDto request, CancellationToken ct)
    {
        var wallet = await GetOrCreateAsync(ownerUserId, ct);
        var now = _time.GetUtcNow();

        var topUp = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            Type = WalletTransactionType.TopUp,
            Status = WalletTransactionStatus.Initiated,
            Amount = request.Amount,
            Currency = wallet.Currency,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.WalletTransactions.Add(topUp);
        await _db.SaveChangesAsync(ct);

        return Result<CardCheckoutDto>.Ok(new CardCheckoutDto
        {
            TransactionId = topUp.Id,
            Provider = _gateway.Provider,
            PublishableKey = _settings.Moyasar.IsConfigured ? _settings.Moyasar.PublishableKey : null,
            Amount = topUp.Amount,
            AmountHalalas = PaymentAmounts.ToHalalas(topUp.Amount),
            Currency = topUp.Currency,
            Description = "شحن المحفظة",
            Metadata = new Dictionary<string, string> { [PaymentAmounts.WalletMetadataKey] = topUp.Id.ToString() },
            SupportedNetworks = ["mada", "visa", "mastercard"]
        });
    }

    public async Task<Result<WalletTopUpResultDto>> ConfirmTopUpAsync(
        Guid ownerUserId, ConfirmCardPaymentDto request, CancellationToken ct)
    {
        var wallet = await GetOrCreateAsync(ownerUserId, ct);
        var topUp = await _db.WalletTransactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TransactionId && t.WalletId == wallet.Id, ct);

        if (topUp is null)
            return Result<WalletTopUpResultDto>.Fail(AppErrorCode.NotFound, "Top-up not found.");

        // A retry after a timeout answers the same thing again.
        if (topUp.Status != WalletTransactionStatus.Initiated)
            return Result<WalletTopUpResultDto>.Ok(await ResultOfAsync(topUp.Id, ct));

        var gatewayId = request.GatewayPaymentId.Trim();

        // One charge credits once, and can't also have paid an installment.
        var usedElsewhere = await _db.WalletTransactions.AnyAsync(t => t.GatewayPaymentId == gatewayId && t.Id != topUp.Id, ct)
                            || await _db.PaymentTransactions.AnyAsync(t => t.GatewayPaymentId == gatewayId, ct);
        if (usedElsewhere)
            return Result<WalletTopUpResultDto>.Fail(AppErrorCode.Conflict, "That charge was already used.");

        var charge = await _gateway.GetPaymentAsync(gatewayId,
            new ExpectedCharge(PaymentAmounts.WalletMetadataKey, topUp.Id, topUp.Amount, topUp.Currency), ct);
        if (charge is null)
            return Result<WalletTopUpResultDto>.Fail(AppErrorCode.NotFound, "The gateway has no such payment.");

        if (Check(charge, topUp) is { } problem)
            return Result<WalletTopUpResultDto>.Fail(AppErrorCode.Conflict, problem);

        if (charge.Status is not ("paid" or "failed"))
            return Result<WalletTopUpResultDto>.Fail(AppErrorCode.Conflict, $"The payment is not complete yet ({charge.Status}).");

        await ApplyAsync(topUp.Id, topUp.WalletId, topUp.Amount, charge, ct);
        return Result<WalletTopUpResultDto>.Ok(await ResultOfAsync(topUp.Id, ct));
    }

    public async Task SettleFromGatewayAsync(GatewayPayment charge, CancellationToken ct)
    {
        if (!charge.Metadata.TryGetValue(PaymentAmounts.WalletMetadataKey, out var raw) || !Guid.TryParse(raw, out var id))
            return;

        var topUp = await _db.WalletTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (topUp is null || topUp.Status != WalletTransactionStatus.Initiated
            || charge.Status is not ("paid" or "failed") || Check(charge, topUp) is not null)
            return;

        if (await _db.PaymentTransactions.AnyAsync(t => t.GatewayPaymentId == charge.Id, ct))
            return;

        await ApplyAsync(topUp.Id, topUp.WalletId, topUp.Amount, charge, ct);
    }

    // Our attempt id in the metadata, the right amount and currency.
    private static string? Check(GatewayPayment charge, WalletTransaction topUp)
    {
        if (!charge.Metadata.TryGetValue(PaymentAmounts.WalletMetadataKey, out var tag) || tag != topUp.Id.ToString())
            return "That charge belongs to a different top-up.";

        if (charge.AmountHalalas != PaymentAmounts.ToHalalas(topUp.Amount)
            || !string.Equals(charge.Currency, topUp.Currency, StringComparison.OrdinalIgnoreCase))
            return "The charged amount does not match the top-up.";

        return null;
    }

    // Settles the top-up and credits the wallet in one database transaction. The status update
    // only matches while the top-up is still Initiated, so the app's confirm and Moyasar's webhook
    // arriving together credit the wallet once.
    private async Task ApplyAsync(Guid topUpId, Guid walletId, decimal amount, GatewayPayment charge, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var paid = charge.Status == "paid";
        var brand = CardLabels.Brand(charge.CardBrand);
        var last4 = CardLabels.LastFour(charge.CardNumber);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var claimed = await _db.WalletTransactions
            .Where(t => t.Id == topUpId && t.Status == WalletTransactionStatus.Initiated)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, paid ? WalletTransactionStatus.Completed : WalletTransactionStatus.Failed)
                .SetProperty(t => t.GatewayPaymentId, charge.Id)
                .SetProperty(t => t.CardBrand, brand)
                .SetProperty(t => t.CardLast4, last4)
                .SetProperty(t => t.FailureReason, paid ? null : charge.Message)
                .SetProperty(t => t.UpdatedAt, now), ct);

        if (claimed == 0)
            return; // someone else settled it a moment ago

        if (paid)
        {
            await _db.Wallets.Where(w => w.Id == walletId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.Balance, w => w.Balance + amount)
                    .SetProperty(w => w.UpdatedAt, now), ct);

            // Read inside the transaction: the row stays locked until commit.
            var balance = await _db.Wallets.Where(w => w.Id == walletId).Select(w => w.Balance).FirstAsync(ct);
            await _db.WalletTransactions.Where(t => t.Id == topUpId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.BalanceAfter, balance), ct);
        }

        await tx.CommitAsync(ct);

        _logger.LogInformation("Wallet top-up {TopUp}: {Status}, {Amount}.", topUpId, charge.Status, amount);
    }

    private async Task<WalletTopUpResultDto> ResultOfAsync(Guid topUpId, CancellationToken ct)
    {
        var row = await _db.WalletTransactions.AsNoTracking()
            .Where(t => t.Id == topUpId)
            .Select(t => new
            {
                t.Id, t.Status, t.CardBrand, t.CardLast4, t.FailureReason,
                Balance = _db.Wallets.Where(w => w.Id == t.WalletId).Select(w => w.Balance).First()
            })
            .FirstAsync(ct);

        return new WalletTopUpResultDto
        {
            TransactionId = row.Id,
            Status = row.Status,
            Balance = row.Balance,
            PaymentMethod = row.Status == WalletTransactionStatus.Completed ? CardLabels.Display(row.CardBrand, row.CardLast4) : null,
            FailureReason = row.FailureReason
        };
    }

    private async Task<Wallet> GetOrCreateAsync(Guid userId, CancellationToken ct)
    {
        var wallet = await _db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet is not null)
            return wallet;

        var now = _time.GetUtcNow();
        wallet = new Wallet { Id = Guid.NewGuid(), UserId = userId, Balance = 0, Currency = "SAR", CreatedAt = now, UpdatedAt = now };
        _db.Wallets.Add(wallet);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two first requests at once: the unique index let one through; use that one.
            _db.Entry(wallet).State = EntityState.Detached;
            return await _db.Wallets.AsNoTracking().FirstAsync(w => w.UserId == userId, ct);
        }

        return wallet;
    }
}
