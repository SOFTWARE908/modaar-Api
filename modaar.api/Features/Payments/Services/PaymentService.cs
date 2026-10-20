using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Payments.Entities;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Users.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Payments.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public PaymentService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<Result<PagedResultDto<PaymentDto>>> ListAsync(
        Guid callerUserId, PaymentListQueryDto query, AccountType activeRole, CancellationToken ct)
    {
        var types = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted)
            .Select(u => u.AccountTypes)
            .FirstOrDefaultAsync(ct);

        if (types is null)
            return Result<PagedResultDto<PaymentDto>>.Fail(AppErrorCode.NotFound, "User not found.");
        var today = SaudiTime.Today(_time);
        var payments = Scope(_db.Payments.AsNoTracking(), callerUserId, activeRole);

        if (!query.IncludeUpcoming)
            payments = payments.Where(p => p.DueDate == null || p.DueDate <= today);

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
            // الأقساط المستحقة أولاً، ثم التاريخ. SQL Server يرتّب NULL آخراً تنازلياً،
            // فالصفوف المرفوعة بلا جدول تنزل تحت المجدولة بدل أن تتصدّرها.
            .OrderByDescending(p => p.DueDate)
            .ThenByDescending(p => p.Date)
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
                Status = p.Status,
                DisplayStatus =
                    p.Status == PaymentStatus.Paid ? PaymentDisplayStatus.Paid
                    : p.Status == PaymentStatus.UnderReview ? PaymentDisplayStatus.UnderReview
                    : p.DueDate != null && p.DueDate < today ? PaymentDisplayStatus.Overdue
                    : PaymentDisplayStatus.Due,
                DaysOverdue =
                    p.Status != PaymentStatus.Paid && p.DueDate != null && p.DueDate < today
                        ? EF.Functions.DateDiffDay(p.DueDate.Value, today)
                        : 0,
                DueDate = p.DueDate,
                PeriodStart = p.PeriodStart,
                PeriodEnd = p.PeriodEnd,
                ServiceFee = p.ServiceFee,
                SadadBillNumber = p.SadadBillNumber,
                PaymentMethod = p.PaymentMethod,
                InvoiceUrl = p.InvoiceUrl
            })
            .ToListAsync(ct);
        return Result<PagedResultDto<PaymentDto>>.Ok(
            new PagedResultDto<PaymentDto> { Items = items, TotalCount = totalCount });
    }

    public async Task<Result<PaymentSummaryDto>> GetSummaryAsync(
        Guid callerUserId, AccountType activeRole, CancellationToken ct)
    {
        var exists = await _db.Users.AnyAsync(u => u.Id == callerUserId && !u.IsDeleted, ct);
        if (!exists)
            return Result<PaymentSummaryDto>.Fail(AppErrorCode.NotFound, "User not found.");

        var payments = Scope(_db.Payments.AsNoTracking(), callerUserId, activeRole);

        // 1 January in Riyadh, not in UTC: a payment made at 01:00 on 1 January local time
        // belongs to the new year.
        var year = SaudiTime.Today(_time).Year;
        var yearStart = new DateTimeOffset(new DateTime(year, 1, 1),
            SaudiTime.Zone.GetUtcOffset(new DateTime(year, 1, 1)));

        var paidThisYear = await payments
            .Where(p => p.Status == PaymentStatus.Paid && p.Date >= yearStart)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        var remaining = await payments
            .Where(p => p.Type == PaymentType.Rent
                     && p.Status != PaymentStatus.Paid
                     && _db.Contracts.Any(c => c.Id == p.ContractId && c.Status == ContractStatus.Active))
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        // Every row is SAR today; read it off the data rather than hardcode it twice.
        var currency = await payments.Select(p => p.Currency).FirstOrDefaultAsync(ct) ?? "SAR";

        return Result<PaymentSummaryDto>.Ok(new PaymentSummaryDto
        {
            PaidThisYear = paidThisYear,
            RemainingOnContract = remaining,
            Currency = currency
        });
    }

    public async Task<Result<InvoiceDetailsDto>> GetInvoiceAsync(
        Guid paymentId, Guid callerUserId, AccountType activeRole, CancellationToken ct)
    {
        var payment = await LoadAccessibleAsync(paymentId, callerUserId, activeRole, ct);
        if (payment.Failure is { } failure)
            return Result<InvoiceDetailsDto>.Fail(failure.ErrorCode!.Value, failure.Error!);

        var p = payment.Payment!;

        var items = await _db.InvoiceItems
            .AsNoTracking()
            .Where(i => i.PaymentId == paymentId)
            .OrderBy(i => i.SortOrder)
            .Select(i => new InvoiceBreakdownItemDto { Label = i.Label, Amount = i.Amount })
            .ToListAsync(ct);

        var tenantName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == p.PayerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        var unit = p.PropertyId is null ? null : await _db.Properties
            .AsNoTracking()
            .Where(prop => prop.Id == p.PropertyId)
            .Select(prop => new { prop.Title, prop.UnitNumber })
            .FirstOrDefaultAsync(ct);

        var contractNumber = p.ContractId is null ? null : await _db.Contracts
            .AsNoTracking()
            .Where(c => c.Id == p.ContractId)
            .Select(c => c.ContractNumber)
            .FirstOrDefaultAsync(ct);

        var transfer = await _db.PaymentTransactions
            .AsNoTracking()
            .Where(t => t.PaymentId == p.Id && t.Channel == PaymentChannel.BankTransfer)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var canReview = false;
        if (p.Status == PaymentStatus.UnderReview && p.PropertyId is { } propertyId)
        {
            var handlers = await PropertyHandlers.ResolveAsync(_db, [propertyId], ct);
            canReview = handlers.TryGetValue(propertyId, out var handler) && handler == callerUserId;
        }

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
            ReceiptNumber = p.ReceiptNumber,
            TransactionId = p.TransactionId,
            PeriodStart = p.PeriodStart,
            PeriodEnd = p.PeriodEnd,
            ServiceFee = p.ServiceFee,
            IsVatExempt = p.IsVatExempt,
            VatAmount = p.VatAmount,
            TenantName = tenantName,
            // "برج النخيل · 402", the same shape the home card uses.
            UnitLabel = unit is null ? null
                : string.IsNullOrWhiteSpace(unit.UnitNumber) ? unit.Title
                : $"{unit.Title} · {unit.UnitNumber}",
            ContractNumber = contractNumber,
            Items = items,
            InvoiceUrl = p.InvoiceUrl,
            Status = p.Status,
            AmountDue = p.Amount + (p.ServiceFee ?? 0) + (p.VatAmount ?? 0),
            BankTransfer = transfer is null ? null : PayService.ToTransferDto(transfer),
            CanReviewTransfer = canReview
        });
    }

    public async Task<Result<string>> GetInvoiceUrlAsync(
        Guid paymentId, Guid callerUserId, AccountType activeRole, CancellationToken ct)
    {
        var payment = await LoadAccessibleAsync(paymentId, callerUserId, activeRole, ct);
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
        Guid paymentId, Guid callerUserId, AccountType activeRole, CancellationToken ct)
    {
        var types = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted)
            .Select(u => u.AccountTypes)
            .FirstOrDefaultAsync(ct);

        if (types is null)
            return new PaymentAccess { Failure = Result<bool>.Fail(AppErrorCode.NotFound, "User not found.") };

        var payment = await Scope(_db.Payments.AsNoTracking(), callerUserId, activeRole)
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
