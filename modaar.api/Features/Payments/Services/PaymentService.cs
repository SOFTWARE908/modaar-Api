using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Payments.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Payments.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly ModaarDbContext _db;

    public PaymentService(ModaarDbContext db) => _db = db;

    public async Task<Result<PagedResultDto<PaymentDto>>> ListAsync(
        Guid callerUserId, PaymentListQueryDto query, CancellationToken ct)
    {
        var accountType = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted)
            .Select(u => (AccountType?)u.AccountType)
            .FirstOrDefaultAsync(ct);

        if (accountType is null)
            return Result<PagedResultDto<PaymentDto>>.Fail(AppErrorCode.NotFound, "User not found.");

        var payments = Scope(_db.Payments.AsNoTracking(), callerUserId, accountType.Value);

        if (query.Type is { } type)
            payments = payments.Where(p => p.Type == type);

        if (query.FromDate is { } from)
        {
            var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            payments = payments.Where(p => p.Date >= fromUtc);
        }

        if (query.ToDate is { } to)
        {
            // Inclusive of the whole end day.
            var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            payments = payments.Where(p => p.Date < toUtc);
        }

        var totalCount = await payments.CountAsync(ct);

        var items = await payments
            .OrderByDescending(p => p.Date)
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                Title = p.Title,
                Amount = p.Amount,
                Currency = p.Currency,
                Date = p.Date,
                Type = p.Type,
                InvoiceUrl = p.InvoiceUrl
            })
            .ToListAsync(ct);

        return Result<PagedResultDto<PaymentDto>>.Ok(
            new PagedResultDto<PaymentDto> { Items = items, TotalCount = totalCount });
    }

    public async Task<Result<InvoiceDetailsDto>> GetInvoiceAsync(
        Guid paymentId, Guid callerUserId, CancellationToken ct)
    {
        var payment = await LoadAccessibleAsync(paymentId, callerUserId, ct);
        if (payment.Failure is { } failure)
            return Result<InvoiceDetailsDto>.Fail(failure.ErrorCode!.Value, failure.Error!);

        var p = payment.Payment!;

        var items = await _db.InvoiceItems
            .AsNoTracking()
            .Where(i => i.PaymentId == paymentId)
            .OrderBy(i => i.SortOrder)
            .Select(i => new InvoiceBreakdownItemDto { Label = i.Label, Amount = i.Amount })
            .ToListAsync(ct);

        return Result<InvoiceDetailsDto>.Ok(new InvoiceDetailsDto
        {
            Id = p.Id,
            Title = p.Title,
            // The payment's own amount, not the sum of the lines: if an upload disagrees with
            // itself, the figure that was actually paid is the one that counts.
            TotalAmount = p.Amount,
            Currency = p.Currency,
            Date = p.Date,
            PaymentMethod = p.PaymentMethod,
            Items = items,
            InvoiceUrl = p.InvoiceUrl
        });
    }

    public async Task<Result<string>> GetInvoiceUrlAsync(
        Guid paymentId, Guid callerUserId, CancellationToken ct)
    {
        var payment = await LoadAccessibleAsync(paymentId, callerUserId, ct);
        if (payment.Failure is { } failure)
            return Result<string>.Fail(failure.ErrorCode!.Value, failure.Error!);

        // No generator exists. InvoiceUrl is whatever was uploaded, and is null until someone
        // does that — so a 404 rather than an empty string.
        return string.IsNullOrWhiteSpace(payment.Payment!.InvoiceUrl)
            ? Result<string>.Fail(AppErrorCode.NotFound, "No invoice document exists for this payment.")
            : Result<string>.Ok(payment.Payment.InvoiceUrl!);
    }

    private IQueryable<Payment> Scope(IQueryable<Payment> payments, Guid callerUserId, AccountType accountType) =>
        accountType switch
        {
            AccountType.Tenant => payments.Where(p => p.PayerUserId == callerUserId),

            AccountType.Owner => payments.Where(p =>
                _db.Properties.Any(prop => prop.Id == p.PropertyId && prop.OwnerUserId == callerUserId)),

            AccountType.Broker => payments.Where(p =>
                _db.Properties.Any(prop => prop.Id == p.PropertyId && prop.BrokerId != null &&
                    _db.Brokers.Any(b => b.Id == prop.BrokerId && b.UserId == callerUserId))),

            // Technicians have no payment history in this product.
            _ => payments.Where(_ => false)
        };

    // Reuses the list scope so a caller can only open an invoice that would appear in their own
    // history — one rule rather than two that can drift apart.
    private async Task<PaymentAccess> LoadAccessibleAsync(
        Guid paymentId, Guid callerUserId, CancellationToken ct)
    {
        var accountType = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted)
            .Select(u => (AccountType?)u.AccountType)
            .FirstOrDefaultAsync(ct);

        if (accountType is null)
            return new PaymentAccess { Failure = Result<bool>.Fail(AppErrorCode.NotFound, "User not found.") };

        var payment = await Scope(_db.Payments.AsNoTracking(), callerUserId, accountType.Value)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

        // Deliberately 404 rather than 403 for someone else's payment: the caller has no
        // business learning that a given payment id exists.
        return payment is null
            ? new PaymentAccess { Failure = Result<bool>.Fail(AppErrorCode.NotFound, "Payment not found.") }
            : new PaymentAccess { Payment = payment };
    }

    private sealed class PaymentAccess
    {
        public Payment? Payment { get; init; }
        public Result<bool>? Failure { get; init; }
    }
}
