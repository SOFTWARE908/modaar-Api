using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Dashboards.Dtos;
using modaar.api.Features.Maintenance;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Maintenance.Services;
using modaar.api.Features.Payments.Dtos;
using modaar.api.Features.Payments.Entities;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Persistence;

namespace modaar.api.Features.Dashboards.Services;

// Pure aggregation over the other slices — no tables of its own.
public sealed class DashboardService : IDashboardService
{
    private const string DefaultCurrency = "SAR";
    private const int PreviewCount = 5;

    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IRequestLanguage _language;

    public DashboardService(ModaarDbContext db, TimeProvider time, IRequestLanguage language)
    {
        _db = db;
        _time = time;
        _language = language;
    }

    public async Task<Result<OwnerDashboardDto>> GetOwnerAsync(Guid userId, AccountType activeRole, CancellationToken ct)
    {
        if (activeRole != AccountType.Owner)
            return Result<OwnerDashboardDto>.Fail(AppErrorCode.Forbidden,
                "Switch to your owner account to open this dashboard.");

        var user = await LoadUserAsync(userId, activeRole, ct);
        if (user is null)
            return Result<OwnerDashboardDto>.Fail(AppErrorCode.NotFound, "User not found.");

        if (user.AccountType != AccountType.Owner)
            return Result<OwnerDashboardDto>.Fail(AppErrorCode.Forbidden, "This dashboard is for owner accounts.");

        var properties = _db.Properties.AsNoTracking().Where(p => !p.IsDeleted && p.OwnerUserId == userId);

        var unitsCount = await properties.CountAsync(ct);

        var activeContracts = await _db.Contracts
            .AsNoTracking()
            .CountAsync(c => c.Status == ContractStatus.Active &&
                             properties.Any(p => p.Id == c.PropertyId), ct);

        var payments = _db.Payments.AsNoTracking()
            .Where(p => properties.Any(prop => prop.Id == p.PropertyId));

        var revenue = await RevenueAsync(payments, ct);
        var collectionRate = await CollectionRateAsync(payments, ct);

        var maintenancePreview = await MaintenancePreviewAsync(
            _db.MaintenanceRequests.AsNoTracking()
                .Where(m => properties.Any(p => p.Id == m.PropertyId)), userId, ct);

        // The brokerage on the owner's portfolio. Taking the first is a simplification: an owner
        // can in principle use different brokers per property, and the spec has one slot.
        var brokerId = await properties
            .Where(p => p.BrokerId != null)
            .Select(p => p.BrokerId)
            .FirstOrDefaultAsync(ct);

        var broker = brokerId is null ? null : await BrokerSummaryAsync(brokerId.Value, ct);

        var stats = new List<DashboardStatDto>
        {
            new() { Icon = "building", Label = DashboardLabels.Units(_language),           Value = unitsCount.ToString() },
            new() { Icon = "contract", Label = DashboardLabels.ActiveContracts(_language), Value = activeContracts.ToString() }
        };


        // Only shown once something has been billed; a rate over an empty denominator would
        // read as 0% rather than "nothing to collect".
        if (collectionRate is { } rate)
            stats.Add(new DashboardStatDto
            {
                Icon = "percent",
                Label = DashboardLabels.CollectionRate(_language),
                Value = $"{rate:0}%"
            });

        var (_, expectedThisMonth) = await MonthAsync(payments, ct);
        var maintenanceCounts = await MaintenanceTaskCountsAsync(
            _db.MaintenanceRequests.AsNoTracking().Where(m => properties.Any(p => p.Id == m.PropertyId)), userId, ct);
        var expiringBy = SaudiTime.Today(_time).AddDays(60);
        var contractsExpiring = await _db.Contracts.AsNoTracking().CountAsync(c =>
            c.Status == ContractStatus.Active && c.EndDate <= expiringBy && properties.Any(p => p.Id == c.PropertyId), ct);

        return Result<OwnerDashboardDto>.Ok(new OwnerDashboardDto
        {
            UserName = user.FullName,
            Revenue = revenue,
            CollectedThisMonth = revenue.Amount,
            ExpectedThisMonth = expectedThisMonth,
            Tasks =
            [
                new() { Type = DashboardTaskType.AwaitingOwnerApproval, Count = maintenanceCounts.GetValueOrDefault(MaintenanceAvailableAction.ApproveQuote) },
                new() { Type = DashboardTaskType.NewRequests, Count = maintenanceCounts.GetValueOrDefault(MaintenanceAvailableAction.Accept) },
                new() { Type = DashboardTaskType.ReadyForTechnician, Count = maintenanceCounts.GetValueOrDefault(MaintenanceAvailableAction.AssignTechnician) },
                new() { Type = DashboardTaskType.ContractsExpiring, Count = contractsExpiring }
            ],
            Stats = stats,
            Broker = broker,
            MaintenancePreview = maintenancePreview,
            TransactionsPreview = await TransactionsPreviewAsync(payments, ct)
        });
    }

    public async Task<Result<TenantDashboardDto>> GetTenantAsync(Guid userId, AccountType activeRole, CancellationToken ct)
    {
        if (activeRole != AccountType.Tenant)
            return Result<TenantDashboardDto>.Fail(AppErrorCode.Forbidden,
                "Switch to your tenant account to open this dashboard.");

        var user = await LoadUserAsync(userId, activeRole, ct);
        if (user is null)
            return Result<TenantDashboardDto>.Fail(AppErrorCode.NotFound, "User not found.");

        if (user.AccountType != AccountType.Tenant)
            return Result<TenantDashboardDto>.Fail(AppErrorCode.Forbidden, "This dashboard is for tenant accounts.");

        // A tenant may hold more than one active lease, and each gets a card. The most recently
        // started one is also CurrentUnit, which older builds read.
        var leases = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.TenantUserId == userId && c.Status == ContractStatus.Active)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new
            {
                c.Id,
                c.PropertyId,
                c.InstallmentAmount,
                c.Currency,
                c.StartDate,
                c.Title,
                c.EndDate,
                Property = _db.Properties.Where(p => p.Id == c.PropertyId)
                    .Select(p => new { p.Title, p.UnitNumber, p.District, p.City, p.Floor }).FirstOrDefault()
            })
            .ToListAsync(ct);
        var today = SaudiTime.Today(_time);

        var units = new List<TenantUnitCardDto>();
        foreach (var lease in leases)
        {

            // The earliest unpaid installment is the one the card is about: an overdue one
            // if any is overdue, otherwise the next one coming.
            var owed = await _db.Payments
                .AsNoTracking()
                .Where(p => p.ContractId == lease.Id
                         && p.Type == PaymentType.Rent
                         && p.Status != PaymentStatus.Paid
                         && p.DueDate != null)
                .OrderBy(p => p.DueDate)
                .Select(p => new { p.DueDate, p.Amount, p.SadadBillNumber })
                .FirstOrDefaultAsync(ct);

            DateOnly dueDate;
            bool isOverdue;
            decimal installment;
            string? sadadBillNumber = null;

            if (owed is not null)
            {
                dueDate = owed.DueDate!.Value;
                isOverdue = dueDate < today;
                installment = owed.Amount;
                sadadBillNumber = owed.SadadBillNumber;
            }
            else
            {
                // No schedule uploaded for this contract. Fall back to deriving the date
                // from the lease's start day of month — right for a plain monthly lease,
                // wrong for anything else, which is why the schedule is preferred.
                var lastDue = PreviousDueDate(lease.StartDate, lease.EndDate, today);
                var nextDue = NextDueDate(lease.StartDate, lease.EndDate, today);

                var lastRentPaidOn = await _db.Payments
                    .AsNoTracking()
                    .Where(p => p.ContractId == lease.Id && p.Type == PaymentType.Rent
                             && p.Status == PaymentStatus.Paid)
                    .OrderByDescending(p => p.Date)
                    .Select(p => (DateTimeOffset?)p.Date)
                    .FirstOrDefaultAsync(ct);

                var settled = lastRentPaidOn is not null &&
                    DateOnly.FromDateTime(lastRentPaidOn.Value.UtcDateTime) >= lastDue;

                isOverdue = lastDue < today && !settled;
                dueDate = isOverdue ? lastDue : nextDue;
                installment = lease.InstallmentAmount;
            }

            units.Add(new TenantUnitCardDto
            {
                ContractId = lease.Id,
                PropertyId = lease.PropertyId,
                UnitTitle = lease.Property?.Title ?? string.Empty,
                PropertyName = lease.Property?.Title ?? string.Empty,
                UnitNumber = lease.Property?.UnitNumber,
                District = lease.Property?.District,
                City = lease.Property?.City,
                Floor = lease.Property?.Floor,
                ContractEndDate = lease.EndDate,
                InstallmentAmount = installment,
                Currency = lease.Currency,
                DueDate = dueDate,
                SadadBillNumber = sadadBillNumber,
                IsOverdue = isOverdue,
                DaysOverdue = isOverdue ? today.DayNumber - dueDate.DayNumber : 0
            });
        }

        var paymentsPreview = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PayerUserId == userId)
            .OrderByDescending(p => p.Date)
            .Take(PreviewCount)
            .Select(p => new PaymentDto
            {
                Id = p.Id,
                Title = p.Title,
                Amount = p.Amount,
                Currency = p.Currency,
                DisplayStatus =
                    p.Status == PaymentStatus.Paid ? PaymentDisplayStatus.Paid
                    : p.Status == PaymentStatus.UnderReview ? PaymentDisplayStatus.UnderReview
                    : p.DueDate != null && p.DueDate < today ? PaymentDisplayStatus.Overdue
                    : PaymentDisplayStatus.Due,
                DaysOverdue =
                    p.Status != PaymentStatus.Paid && p.DueDate != null && p.DueDate < today
                        ? EF.Functions.DateDiffDay(p.DueDate.Value, today)
                        : 0,
                Date = p.Date,
                Type = p.Type,
                Status = p.Status,

                ServiceFee = p.ServiceFee,
                SadadBillNumber = p.SadadBillNumber,
                PaymentMethod = p.PaymentMethod,
                InvoiceUrl = p.InvoiceUrl
            })
            .ToListAsync(ct);

        var categoryRows = await _db.MaintenanceServiceTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new { t.Id, t.Code, t.NameAr, t.NameEn, t.IconUrl, t.SortOrder })
            .ToListAsync(ct);

        var categories = categoryRows.Select(t => new MaintenanceServiceTypeDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = _language.Pick(t.NameAr, t.NameEn),
            IconUrl = t.IconUrl,
            SortOrder = t.SortOrder
        }).ToList();

        return Result<TenantDashboardDto>.Ok(new TenantDashboardDto
        {
            UserName = user.FullName,
            CurrentUnit = units.FirstOrDefault(),
            Units = units,
            // Server-driven so a category can be switched off without shipping a new build.
            MaintenanceCategories = categories,
            PaymentsPreview = paymentsPreview
        });
    }

    public async Task<Result<BrokerDashboardDto>> GetBrokerAsync(Guid userId, AccountType activeRole, CancellationToken ct)
    {
        if (activeRole != AccountType.Broker)
            return Result<BrokerDashboardDto>.Fail(AppErrorCode.Forbidden,
                "Switch to your broker account to open this dashboard.");

        var user = await LoadUserAsync(userId, activeRole, ct);
        if (user is null)
            return Result<BrokerDashboardDto>.Fail(AppErrorCode.NotFound, "User not found.");

        var brokerId = await _db.Brokers
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

        if (brokerId is null)
            return Result<BrokerDashboardDto>.Fail(AppErrorCode.Forbidden, "This dashboard is for broker accounts.");

        var managed = _db.Properties.AsNoTracking().Where(p => !p.IsDeleted && p.BrokerId == brokerId);

        var unitsManaged = await managed.CountAsync(ct);

        var activeContracts = await _db.Contracts
            .AsNoTracking()
            .CountAsync(c => c.BrokerId == brokerId && c.Status == ContractStatus.Active, ct);

        // "Closed deals" read as leases that started this month, since a brokerage's deal is a
        // signed lease. No commission data exists to count anything else.
        var today = SaudiTime.Today(_time);
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var closedThisMonth = await _db.Contracts
            .AsNoTracking()
            .CountAsync(c => c.BrokerId == brokerId && c.StartDate >= monthStart && c.StartDate <= today, ct);

        var payments = _db.Payments.AsNoTracking()
            .Where(p => managed.Any(prop => prop.Id == p.PropertyId));

        var collectionRate = await CollectionRateAsync(payments, ct);

        var clients = await managed
            .GroupBy(p => p.OwnerUserId)
            .Select(g => new { OwnerUserId = g.Key, UnitsCount = g.Count() })
            .OrderByDescending(x => x.UnitsCount)
            .Take(PreviewCount)
            .ToListAsync(ct);

        var clientIds = clients.Select(c => c.OwnerUserId).ToList();

        var clientNames = await _db.Users
            .AsNoTracking()
            .Where(u => clientIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName })
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var maintenancePreview = await MaintenancePreviewAsync(
            _db.MaintenanceRequests.AsNoTracking()
                .Where(m => managed.Any(p => p.Id == m.PropertyId)), userId, ct);

        var stats = new List<DashboardStatDto>
        {
            new() { Icon = "building", Label = DashboardLabels.UnitsManaged(_language),    Value = unitsManaged.ToString() },
            new() { Icon = "contract", Label = DashboardLabels.ActiveContracts(_language), Value = activeContracts.ToString() }
        };

        if (collectionRate is { } rate)
            stats.Add(new DashboardStatDto
            {
                Icon = "percent",
                Label = DashboardLabels.CollectionRate(_language),
                Value = $"{rate:0}%"
            });

        var (collected, expected) = await MonthAsync(payments, ct);
        var counts = await MaintenanceTaskCountsAsync(
            _db.MaintenanceRequests.AsNoTracking().Where(m => managed.Any(p => p.Id == m.PropertyId)), userId, ct);
        var lateRent = payments.Where(p => p.Type == PaymentType.Rent && p.Status == PaymentStatus.Pending
                                           && p.DueDate != null && p.DueDate < today);
        var lateCount = await lateRent.CountAsync(ct);
        var lateAmount = await lateRent.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return Result<BrokerDashboardDto>.Ok(new BrokerDashboardDto
        {
            UserName = user.FullName,
            Collected = collected,
            Expected = expected,
            Tasks =
            [
                new() { Type = DashboardTaskType.NeedsQuote, Count = counts.GetValueOrDefault(MaintenanceAvailableAction.SubmitQuote) },
                new() { Type = DashboardTaskType.ReadyForTechnician, Count = counts.GetValueOrDefault(MaintenanceAvailableAction.AssignTechnician) },
                new() { Type = DashboardTaskType.NewRequests, Count = counts.GetValueOrDefault(MaintenanceAvailableAction.Accept) },
                new() { Type = DashboardTaskType.LateRents, Count = lateCount, Amount = lateAmount }
            ],
            ClosedDeals = new BrokerDealsSummaryDto { Count = closedThisMonth, PeriodLabel = DashboardLabels.ThisMonth(_language) },
            Stats = stats,
            MaintenancePreview = maintenancePreview,
            ClientsPreview = clients.Select(c => new BrokerClientDto
            {
                Id = c.OwnerUserId,
                Name = clientNames.GetValueOrDefault(c.OwnerUserId),
                UnitsCount = c.UnitsCount
            }).ToList(),
            TransactionsPreview = await TransactionsPreviewAsync(payments, ct)
        });
    }

    // Rent paid this month (by payment date) and rent due this month (by due date, any status),
    // in Riyadh months.
    private async Task<(decimal Collected, decimal Expected)> MonthAsync(IQueryable<Payment> payments, CancellationToken ct)
    {
        var today = SaudiTime.Today(_time);
        var first = new DateOnly(today.Year, today.Month, 1);
        var next = first.AddMonths(1);
        var start = first.ToDateTime(TimeOnly.MinValue);
        var from = new DateTimeOffset(start, SaudiTime.Zone.GetUtcOffset(start));
        var to = from.AddMonths(1);

        var collected = await payments
            .Where(p => p.Status == PaymentStatus.Paid && p.Date >= from && p.Date < to)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var expected = await payments
            .Where(p => p.Type == PaymentType.Rent && p.DueDate != null && p.DueDate >= first && p.DueDate < next)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return (collected, expected);
    }

    // How many open requests offer the caller each action. Uses MaintenanceStages.Available, the
    // rule behind every request's availableActions, so the counts match the buttons.
    private async Task<Dictionary<MaintenanceAvailableAction, int>> MaintenanceTaskCountsAsync(
        IQueryable<Features.Maintenance.Entities.MaintenanceRequest> scoped, Guid callerUserId, CancellationToken ct)
    {
        var open = await scoped
            .Where(m => m.Status == MaintenanceRequestStatus.Pending || m.Status == MaintenanceRequestStatus.Accepted)
            .Select(m => new
            {
                m.Status,
                m.SubscriptionPlan,
                m.QuoteRequired,
                m.TenantUserId,
                HasQuote = m.QuotedAt != null,
                QuoteApproved = m.QuoteApprovedAt != null,
                HasTechnician = m.AssignedTechnicianId != null,
                Property = _db.Properties.Where(p => p.Id == m.PropertyId).Select(p => new
                {
                    p.OwnerUserId,
                    HasBroker = p.BrokerId != null,
                    BrokerUserId = _db.Brokers.Where(b => b.Id == p.BrokerId).Select(b => (Guid?)b.UserId).FirstOrDefault()
                }).FirstOrDefault()
            })
            .ToListAsync(ct);

        return open
            .SelectMany(m =>
            {
                var facts = new MaintenanceFacts(m.Status, m.SubscriptionPlan, m.Property?.HasBroker ?? false,
                    m.QuoteRequired, m.HasQuote, m.QuoteApproved, m.HasTechnician, false);
                var stage = MaintenanceStages.Derive(facts);
                return MaintenanceStages.Available(facts, new MaintenanceCaller(
                        m.TenantUserId == callerUserId, m.Property?.OwnerUserId == callerUserId,
                        m.Property?.BrokerUserId == callerUserId))
                    // Revising a quote that waits for the owner, or an optional quote, isn't
                    // "needs a quote": only a request stuck at the pricing step is.
                    .Where(a => a != MaintenanceAvailableAction.SubmitQuote || stage == MaintenanceStage.AwaitingQuote);
            })
            .GroupBy(a => a)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    // Revenue for the current calendar month, with the change against the previous one.
    private async Task<OwnerRevenueDto> RevenueAsync(IQueryable<Payment> payments, CancellationToken ct)
    {
        var today = SaudiTime.Today(_time);

        var monthStart = new DateTimeOffset(new DateOnly(today.Year, today.Month, 1)
            .ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var previousStart = monthStart.AddMonths(-1);

        var paid = payments.Where(p => p.Status == PaymentStatus.Paid);

        var current = await paid
            .Where(p => p.Date >= monthStart)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var previous = await paid
            .Where(p => p.Date >= previousStart && p.Date < monthStart)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        // Null rather than 100% when the previous month was zero: there is no percentage change
        // from nothing, and showing one would overstate a first month of income.
        decimal? trend = previous == 0m
            ? null
            : Math.Round((current - previous) / previous * 100m, 1);

        return new OwnerRevenueDto { Amount = current, Currency = DefaultCurrency, TrendPercent = trend };
    }

    // Paid against everything billed. Null when nothing has been billed at all.
    private static async Task<decimal?> CollectionRateAsync(IQueryable<Payment> payments, CancellationToken ct)
    {
        var billed = await payments.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        if (billed == 0m) return null;

        var collected = await payments
            .Where(p => p.Status == PaymentStatus.Paid)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        return Math.Round(collected / billed * 100m, 0);
    }

    private Task<List<TransactionPreviewDto>> TransactionsPreviewAsync(
        IQueryable<Payment> payments, CancellationToken ct) =>
        payments
            .OrderByDescending(p => p.Date)
            .Take(PreviewCount)
            .Select(p => new TransactionPreviewDto
            {
                Amount = p.Amount,
                Currency = p.Currency,
                Status = p.Status.ToString(),
                SenderName = _db.Users.Where(u => u.Id == p.PayerUserId)
                    .Select(u => u.FullName).FirstOrDefault(),
                PropertyName = _db.Properties.Where(prop => prop.Id == p.PropertyId)
                    .Select(prop => prop.Title).FirstOrDefault(),
                Date = p.Date,
                InvoiceId = p.Id
            })
            .ToListAsync(ct);

    // The next monthly anniversary of the lease start on or after today, clamped to the lease
    // end. Clamps the day for short months, so a lease starting on the 31st bills on the 28th
    // in February rather than skipping.

    private static DateOnly NextDueDate(DateOnly start, DateOnly end, DateOnly today)
    {
        var candidate = BuildDue(today.Year, today.Month, start.Day);

        if (candidate < today)
            candidate = BuildDue(
                today.Month == 12 ? today.Year + 1 : today.Year,
                today.Month == 12 ? 1 : today.Month + 1,
                start.Day);

        return candidate > end ? end : candidate;
    }
    private static DateOnly BuildDue(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));


    private static DateOnly PreviousDueDate(DateOnly start, DateOnly end, DateOnly today)
    {
        var candidate = BuildDue(today.Year, today.Month, start.Day);

        if (candidate > today)
            candidate = BuildDue(
                today.Month == 1 ? today.Year - 1 : today.Year,
                today.Month == 1 ? 12 : today.Month - 1,
                start.Day);

        // Never before the lease began: nothing is owed for a month the tenant was not in.
        if (candidate < start) return start;

        return candidate > end ? end : candidate;
    }

    // Same projection for both dashboards so their maintenance cards cannot drift apart.
    private async Task<List<MaintenanceRequestListItemDto>> MaintenancePreviewAsync(
        IQueryable<Features.Maintenance.Entities.MaintenanceRequest> scoped, Guid callerUserId, CancellationToken ct) =>
        await MaintenanceListProjection.ToListItemsAsync(
            scoped.OrderByDescending(m => m.CreatedAt).Take(PreviewCount), _db, _language, callerUserId, ct);

    private async Task<BrokerSummaryDto?> BrokerSummaryAsync(Guid brokerId, CancellationToken ct)
    {
        var row = await _db.Brokers
            .AsNoTracking()
            .Where(b => b.Id == brokerId)
            .Select(b => new
            {
                b.Id,
                b.SubtitleAr,
                b.SubtitleEn,
                b.RatingAverage,
                b.ReviewsCount,
                b.IsVerified,
                Name = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.FullName).FirstOrDefault(),
                LogoUrl = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new BrokerSummaryDto
        {
            Id = row.Id,
            Name = row.Name ?? string.Empty,
            LogoUrl = row.LogoUrl,
            Subtitle = _language.Pick(row.SubtitleAr, row.SubtitleEn),
            RatingAverage = row.RatingAverage,
            ReviewsCount = row.ReviewsCount,
            IsVerified = row.IsVerified
        };
    }

    private async Task<UserSummary?> LoadUserAsync(Guid userId, AccountType accountType, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new UserSummary(u.FullName, accountType))
            .FirstOrDefaultAsync(ct);

    private sealed record UserSummary(string? FullName, AccountType AccountType);
}
