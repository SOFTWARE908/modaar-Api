using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using modaar.api.Common.Results;
using modaar.api.Features.Notifications.Services;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Payments.Entities;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Payments.Gateway;
using modaar.api.Features.Users.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Payments.Services;

public interface IPayService
{
    Task<Result<CardCheckoutDto>> StartCardCheckoutAsync(Guid paymentId, Guid callerUserId, CancellationToken ct);
    Task<Result<PaymentResultDto>> ConfirmCardAsync(Guid paymentId, Guid callerUserId, ConfirmCardPaymentDto request, CancellationToken ct);
    Task HandleGatewayNotificationAsync(string gatewayPaymentId, CancellationToken ct);

    BankTransferInfoDto GetBankTransferInfo();
    Task<Result<BankTransferDto>> SubmitBankTransferAsync(Guid paymentId, Guid callerUserId, IFormFile receipt, CancellationToken ct);
    Task<Result<bool>> ApproveBankTransferAsync(Guid paymentId, Guid callerUserId, CancellationToken ct);
    Task<Result<bool>> RejectBankTransferAsync(Guid paymentId, Guid callerUserId, RejectBankTransferDto request, CancellationToken ct);
}

// Paying an installment that is owed (Status = Pending).
//
// Card (mada, Visa, Mastercard) through Moyasar:
//   1. checkout  → we record an attempt and tell the app the amount and the metadata to send
//   2. the app charges the card with the Moyasar SDK (card details never reach our server)
//   3. confirm   → we fetch that charge from Moyasar with the secret key and check it ourselves:
//                  paid, the right amount, the right currency, our attempt id in its metadata
//   The Moyasar webhook runs step 3 too, for when the app closes before confirming.
//
// Bank transfer to the platform's account:
//   tenant uploads the receipt → UnderReview → the owner or broker running the property
//   approves (Paid) or rejects with a reason (owed again).
public sealed class PayService : IPayService
{
    private const long MaxReceiptBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> ReceiptExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".heic", ".pdf" };

    private static readonly string[] CardNetworks = ["mada", "visa", "mastercard"];

    private readonly ModaarDbContext _db;
    private readonly IPaymentGatewayClient _gateway;
    private readonly modaar.api.Features.Wallets.Services.IWalletService _wallet;
    private readonly PaymentSettings _settings;
    private readonly INotificationService _notifications;
    private readonly IHttpContextAccessor _http;
    private readonly TimeProvider _time;
    private readonly ILogger<PayService> _logger;
    private readonly string _uploadsRoot;

    public PayService(
        ModaarDbContext db,
        IPaymentGatewayClient gateway,
        modaar.api.Features.Wallets.Services.IWalletService wallet,
        IOptions<PaymentSettings> settings,
        INotificationService notifications,
        IHttpContextAccessor http,
        TimeProvider time,
        IConfiguration config,
        ILogger<PayService> logger)
    {
        _db = db;
        _gateway = gateway;
        _wallet = wallet;
        _settings = settings.Value;
        _notifications = notifications;
        _http = http;
        _time = time;
        _logger = logger;
        _uploadsRoot = config["Uploads:Root"] ?? throw new InvalidOperationException("Uploads:Root is not configured.");
    }

    // ---- card ----

    public async Task<Result<CardCheckoutDto>> StartCardCheckoutAsync(Guid paymentId, Guid callerUserId, CancellationToken ct)
    {
        var payment = await LoadPayableAsync(paymentId, callerUserId, ct);
        if (payment.Failure is { } failure) return Fail<CardCheckoutDto>(failure);

        var p = payment.Payment!;
        var now = _time.GetUtcNow();

        var attempt = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            PaymentId = p.Id,
            PayerUserId = callerUserId,
            Channel = PaymentChannel.Card,
            Status = PaymentTransactionStatus.Initiated,
            Amount = TotalOf(p),
            Currency = p.Currency,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PaymentTransactions.Add(attempt);
        await _db.SaveChangesAsync(ct);

        return Result<CardCheckoutDto>.Ok(new CardCheckoutDto
        {
            TransactionId = attempt.Id,
            Provider = _gateway.Provider,
            PublishableKey = _settings.Moyasar.IsConfigured ? _settings.Moyasar.PublishableKey : null,
            Amount = attempt.Amount,
            AmountHalalas = PaymentAmounts.ToHalalas(attempt.Amount),
            Currency = attempt.Currency,
            Description = p.Title,
            Metadata = new Dictionary<string, string> { [PaymentAmounts.TransactionMetadataKey] = attempt.Id.ToString() },
            SupportedNetworks = CardNetworks
        });
    }

    public async Task<Result<PaymentResultDto>> ConfirmCardAsync(
        Guid paymentId, Guid callerUserId, ConfirmCardPaymentDto request, CancellationToken ct)
    {
        var attempt = await _db.PaymentTransactions.FirstOrDefaultAsync(t =>
            t.Id == request.TransactionId && t.PaymentId == paymentId && t.PayerUserId == callerUserId
            && t.Channel == PaymentChannel.Card, ct);

        if (attempt is null)
            return Result<PaymentResultDto>.Fail(AppErrorCode.NotFound, "Checkout not found.");

        // Confirming twice (a retry after a timeout) answers the same thing again.
        if (attempt.Status is PaymentTransactionStatus.Paid or PaymentTransactionStatus.Failed)
            return Result<PaymentResultDto>.Ok(await ResultOfAsync(attempt, ct));

        var failure = await SettleCardAsync(attempt, request.GatewayPaymentId.Trim(), ct);
        return failure is not null
            ? Fail<PaymentResultDto>(failure)
            : Result<PaymentResultDto>.Ok(await ResultOfAsync(attempt, ct));
    }

    // The webhook only tells us a charge changed. Everything else is read back from Moyasar, so a
    // forged webhook can at most make us look up a real payment.
    public async Task HandleGatewayNotificationAsync(string gatewayPaymentId, CancellationToken ct)
    {
        var charge = await _gateway.GetPaymentAsync(gatewayPaymentId, ExpectedCharge.Unknown, ct);

        // A wallet top-up rather than an installment.
        if (charge is not null && charge.Metadata.ContainsKey(PaymentAmounts.WalletMetadataKey))
        {
            await _wallet.SettleFromGatewayAsync(charge, ct);
            return;
        }

        if (charge is null || !charge.Metadata.TryGetValue(PaymentAmounts.TransactionMetadataKey, out var raw)
            || !Guid.TryParse(raw, out var attemptId))
        {
            _logger.LogWarning("Gateway notification for {GatewayId}: not one of ours.", gatewayPaymentId);
            return;
        }

        var attempt = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == attemptId, ct);
        if (attempt is null || attempt.Status is PaymentTransactionStatus.Paid or PaymentTransactionStatus.Failed)
            return;

        await SettleCardAsync(attempt, gatewayPaymentId, ct, charge);
    }

    // Null when settled (paid or declined); a Failure when the charge can't be accepted or isn't done.
    private async Task<Failure?> SettleCardAsync(
        PaymentTransaction attempt, string gatewayPaymentId, CancellationToken ct, GatewayPayment? charge = null)
    {
        // One charge pays one installment: refuse an id another attempt already used.
        var usedElsewhere = await _db.PaymentTransactions.AnyAsync(t =>
                                t.GatewayPaymentId == gatewayPaymentId && t.Id != attempt.Id, ct)
                            || await _db.WalletTransactions.AnyAsync(t => t.GatewayPaymentId == gatewayPaymentId, ct);
        if (usedElsewhere)
            return new Failure(AppErrorCode.Conflict, "That charge was already used for another payment.");

        charge ??= await _gateway.GetPaymentAsync(gatewayPaymentId,
            new ExpectedCharge(PaymentAmounts.TransactionMetadataKey, attempt.Id, attempt.Amount, attempt.Currency), ct);
        if (charge is null)
            return new Failure(AppErrorCode.NotFound, "The gateway has no such payment.");

        // Each check guards a different trick: someone else's charge, a cheaper charge, a charge
        // in another currency.
        if (!charge.Metadata.TryGetValue(PaymentAmounts.TransactionMetadataKey, out var tag) || tag != attempt.Id.ToString())
            return new Failure(AppErrorCode.Conflict, "That charge belongs to a different checkout.");

        if (charge.AmountHalalas != PaymentAmounts.ToHalalas(attempt.Amount)
            || !string.Equals(charge.Currency, attempt.Currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Charge {GatewayId} is {Amount} {Currency}; attempt {Attempt} expected {Expected} {ExpectedCurrency}.",
                charge.Id, charge.AmountHalalas, charge.Currency, attempt.Id, PaymentAmounts.ToHalalas(attempt.Amount), attempt.Currency);
            return new Failure(AppErrorCode.Conflict, "The charged amount does not match the payment.");
        }

        var now = _time.GetUtcNow();

        switch (charge.Status)
        {
            case "paid":
                attempt.GatewayPaymentId = charge.Id;
                attempt.CardBrand = CardLabels.Brand(charge.CardBrand);
                attempt.CardLast4 = CardLabels.LastFour(charge.CardNumber);
                await MarkPaidAsync(attempt, $"{attempt.CardBrand} •••• {attempt.CardLast4}".Trim(), charge.Id, now, ct);
                return null;

            case "failed":
                attempt.GatewayPaymentId = charge.Id;
                attempt.Status = PaymentTransactionStatus.Failed;
                attempt.FailureReason = charge.Message;
                attempt.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
                return null;

            default:
                // Still "initiated": 3-D Secure not finished. Leave the attempt open.
                return new Failure(AppErrorCode.Conflict, $"The payment is not complete yet ({charge.Status}).");
        }
    }

    // ---- bank transfer ----

    public BankTransferInfoDto GetBankTransferInfo() => new()
    {
        BankName = _settings.BankTransfer.BankName,
        AccountName = _settings.BankTransfer.AccountName,
        Iban = _settings.BankTransfer.Iban
    };

    public async Task<Result<BankTransferDto>> SubmitBankTransferAsync(
        Guid paymentId, Guid callerUserId, IFormFile receipt, CancellationToken ct)
    {
        if (receipt.Length == 0)
            return Result<BankTransferDto>.Fail(AppErrorCode.ValidationFailed, "The file is empty.");
        if (receipt.Length > MaxReceiptBytes)
            return Result<BankTransferDto>.Fail(AppErrorCode.ValidationFailed, "The file exceeds 10 MB.");

        var extension = Path.GetExtension(receipt.FileName);
        if (string.IsNullOrEmpty(extension) || !ReceiptExtensions.Contains(extension))
            return Result<BankTransferDto>.Fail(AppErrorCode.ValidationFailed, "The receipt must be an image or a PDF.");

        var payment = await LoadPayableAsync(paymentId, callerUserId, ct, tracked: true);
        if (payment.Failure is { } failure) return Fail<BankTransferDto>(failure);
        var p = payment.Payment!;

        // The stored name comes from a server id only; the client's file name is a label.
        var id = Guid.NewGuid();
        var storedName = $"{id:N}{extension.ToLowerInvariant()}";
        var folder = Path.Combine(_uploadsRoot, "receipts");
        Directory.CreateDirectory(folder);
        await using (var stream = new FileStream(Path.Combine(folder, storedName), FileMode.CreateNew))
            await receipt.CopyToAsync(stream, ct);

        var request = _http.HttpContext!.Request;
        var now = _time.GetUtcNow();
        var transfer = new PaymentTransaction
        {
            Id = id,
            PaymentId = p.Id,
            PayerUserId = callerUserId,
            Channel = PaymentChannel.BankTransfer,
            Status = PaymentTransactionStatus.UnderReview,
            Amount = TotalOf(p),
            Currency = p.Currency,
            ReceiptUrl = $"{request.Scheme}://{request.Host}/uploads/receipts/{storedName}",
            ReceiptFileName = Path.GetFileName(receipt.FileName),
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.PaymentTransactions.Add(transfer);

        p.Status = PaymentStatus.UnderReview;
        p.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var (subject, handler) = await SubjectAndHandlerAsync(p, ct);
        if (handler is { } handlerId)
            await _notifications.NotifyAsync([handlerId], PaymentNotifications.TransferToReview(subject), ct);

        return Result<BankTransferDto>.Ok(ToTransferDto(transfer));
    }

    public async Task<Result<bool>> ApproveBankTransferAsync(Guid paymentId, Guid callerUserId, CancellationToken ct)
    {
        var review = await LoadReviewAsync(paymentId, callerUserId, ct);
        if (review.Failure is { } failure) return Fail<bool>(failure);

        var now = _time.GetUtcNow();
        review.Transfer!.ReviewedByUserId = callerUserId;
        review.Transfer.ReviewedAt = now;
        await MarkPaidAsync(review.Transfer, "تحويل بنكي", null, now, ct);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> RejectBankTransferAsync(
        Guid paymentId, Guid callerUserId, RejectBankTransferDto request, CancellationToken ct)
    {
        var review = await LoadReviewAsync(paymentId, callerUserId, ct);
        if (review.Failure is { } failure) return Fail<bool>(failure);

        var now = _time.GetUtcNow();
        var transfer = review.Transfer!;
        transfer.Status = PaymentTransactionStatus.Rejected;
        transfer.RejectionReason = request.Reason.Trim();
        transfer.ReviewedByUserId = callerUserId;
        transfer.ReviewedAt = now;
        transfer.UpdatedAt = now;

        // Owed again: the tenant can send a new receipt or pay by card.
        review.Payment!.Status = PaymentStatus.Pending;
        review.Payment.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        var (subject, _) = await SubjectAndHandlerAsync(review.Payment, ct);
        await _notifications.NotifyAsync([review.Payment.PayerUserId],
            PaymentNotifications.TransferRejected(subject, transfer.RejectionReason), ct);

        return Result<bool>.Ok(true);
    }

    // ---- shared ----

    private async Task MarkPaidAsync(PaymentTransaction attempt, string methodLabel, string? gatewayId, DateTimeOffset now, CancellationToken ct)
    {
        attempt.Status = PaymentTransactionStatus.Paid;
        attempt.UpdatedAt = now;

        var payment = await _db.Payments.FirstAsync(p => p.Id == attempt.PaymentId, ct);

        // Two attempts both succeeding (two phones, or a card after a transfer) means the tenant
        // paid twice. Record both; the second needs a refund by hand.
        if (payment.Status == PaymentStatus.Paid)
        {
            _logger.LogError("Payment {PaymentId} was already paid; attempt {Attempt} charged again and needs a refund.",
                payment.Id, attempt.Id);
            await _db.SaveChangesAsync(ct);
            return;
        }

        payment.Status = PaymentStatus.Paid;
        payment.Date = now;
        payment.PaymentMethod = methodLabel;
        payment.TransactionId = gatewayId ?? payment.TransactionId;
        payment.ReceiptNumber ??= $"RCPT-{attempt.Id.ToString("N")[..10].ToUpperInvariant()}";
        payment.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Payment {PaymentId} paid by {Channel} (attempt {Attempt}).", payment.Id, attempt.Channel, attempt.Id);

        var (subject, _) = await SubjectAndHandlerAsync(payment, ct);
        await _notifications.NotifyAsync([payment.PayerUserId], PaymentNotifications.Received(subject), ct);
    }

    // What the tenant is charged: the installment plus whatever fee and VAT the row carries.
    private static decimal TotalOf(Payment p) => p.Amount + (p.ServiceFee ?? 0) + (p.VatAmount ?? 0);

    private async Task<PayableAccess> LoadPayableAsync(Guid paymentId, Guid callerUserId, CancellationToken ct, bool tracked = false)
    {
        var query = tracked ? _db.Payments : _db.Payments.AsNoTracking();
        var payment = await query.FirstOrDefaultAsync(p => p.Id == paymentId && p.PayerUserId == callerUserId, ct);

        // Someone else's payment looks the same as a missing one.
        if (payment is null)
            return new PayableAccess { Failure = new Failure(AppErrorCode.NotFound, "Payment not found.") };

        return payment.Status switch
        {
            PaymentStatus.Pending => new PayableAccess { Payment = payment },
            PaymentStatus.Paid => new PayableAccess { Failure = new Failure(AppErrorCode.Conflict, "This payment is already paid.") },
            PaymentStatus.UnderReview => new PayableAccess { Failure = new Failure(AppErrorCode.Conflict, "A bank transfer for this payment is being reviewed.") },
            _ => new PayableAccess { Failure = new Failure(AppErrorCode.Conflict, $"This payment is {payment.Status} and can't be paid.") }
        };
    }

    // The latest transfer under review, and only for whoever runs the property.
    private async Task<ReviewAccess> LoadReviewAsync(Guid paymentId, Guid callerUserId, CancellationToken ct)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment is null)
            return new ReviewAccess { Failure = new Failure(AppErrorCode.NotFound, "Payment not found.") };

        var (_, handler) = await SubjectAndHandlerAsync(payment, ct);
        if (handler != callerUserId)
            return new ReviewAccess { Failure = new Failure(AppErrorCode.Forbidden, "Only the owner or broker running this property can review its transfers.") };

        var transfer = await _db.PaymentTransactions
            .Where(t => t.PaymentId == paymentId && t.Channel == PaymentChannel.BankTransfer
                     && t.Status == PaymentTransactionStatus.UnderReview)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (transfer is null || payment.Status != PaymentStatus.UnderReview)
            return new ReviewAccess { Failure = new Failure(AppErrorCode.Conflict, "There is no bank transfer waiting for review.") };

        return new ReviewAccess { Payment = payment, Transfer = transfer };
    }

    private async Task<(PaymentNotifications.Subject Subject, Guid? Handler)> SubjectAndHandlerAsync(Payment p, CancellationToken ct)
    {
        var propertyId = p.PropertyId ?? await _db.Contracts
            .Where(c => c.Id == p.ContractId).Select(c => (Guid?)c.PropertyId).FirstOrDefaultAsync(ct);

        var title = propertyId is null ? null
            : await _db.Properties.Where(x => x.Id == propertyId).Select(x => x.Title).FirstOrDefaultAsync(ct);
        var tenantName = await _db.Users.Where(u => u.Id == p.PayerUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

        Guid? handler = null;
        if (propertyId is { } pid)
        {
            var handlers = await PropertyHandlers.ResolveAsync(_db, [pid], ct);
            if (handlers.TryGetValue(pid, out var h)) handler = h;
        }

        return (new PaymentNotifications.Subject(p.Id, title ?? string.Empty, TotalOf(p), p.DueDate, tenantName), handler);
    }

    private async Task<PaymentResultDto> ResultOfAsync(PaymentTransaction attempt, CancellationToken ct)
    {
        var payment = await _db.Payments.AsNoTracking()
            .Where(p => p.Id == attempt.PaymentId)
            .Select(p => new { p.PaymentMethod, p.ReceiptNumber })
            .FirstAsync(ct);

        var paid = attempt.Status == PaymentTransactionStatus.Paid;
        return new PaymentResultDto
        {
            PaymentId = attempt.PaymentId,
            Status = attempt.Status,
            PaymentMethod = paid ? payment.PaymentMethod : null,
            ReceiptNumber = paid ? payment.ReceiptNumber : null,
            FailureReason = attempt.FailureReason
        };
    }

    public static BankTransferDto ToTransferDto(PaymentTransaction t) => new()
    {
        TransactionId = t.Id,
        Status = t.Status,
        ReceiptUrl = t.ReceiptUrl!,
        SubmittedAt = t.CreatedAt,
        RejectionReason = t.RejectionReason
    };

    private static Result<T> Fail<T>(Failure f) => Result<T>.Fail(f.Code, f.Message);

    private sealed record Failure(AppErrorCode Code, string Message);

    private sealed class PayableAccess
    {
        public Payment? Payment { get; init; }
        public Failure? Failure { get; init; }
    }

    private sealed class ReviewAccess
    {
        public Payment? Payment { get; init; }
        public PaymentTransaction? Transfer { get; init; }
        public Failure? Failure { get; init; }
    }
}
