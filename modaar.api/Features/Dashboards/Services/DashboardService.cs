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

    public async Task<Result<OwnerDashboardDto>> GetOwnerAsync(Guid userId, CancellationToken ct)
    {
        var user = await LoadUserAsync(userId, ct);
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
                .Where(m => properties.Any(p => p.Id == m.PropertyId)), ct);

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

        return Result<OwnerDashboardDto>.Ok(new OwnerDashboardDto
        {
            UserName = user.FullName,
            Revenue = revenue,
            Stats = stats,
            Broker = broker,
            MaintenancePreview = maintenancePreview,
            TransactionsPreview = await TransactionsPreviewAsync(payments, ct)
        });
    }

    public async Task<Result<TenantDashboardDto>> GetTenantAsync(Guid userId, CancellationToken ct)
    {
        var user = await LoadUserAsync(userId, ct);
        if (user is null)
            return Result<TenantDashboardDto>.Fail(AppErrorCode.NotFound, "User not found.");

        if (user.AccountType != AccountType.Tenant)
            return Result<TenantDashboardDto>.Fail(AppErrorCode.Forbidden, "This dashboard is for tenant accounts.");

        // A tenant may hold more than one active lease; the card has one slot, so the most
        // recently started wins.
        var lease = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.TenantUserId == userId && c.Status == ContractStatus.Active)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new
            {
                c.Id,
                c.PropertyId,
                c.MonthlyAmount,
                c.Currency,
                c.StartDate,
                c.EndDate,
                PropertyTitle = _db.Properties.Where(p => p.Id == c.PropertyId)
                    .Select(p => p.Title).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        TenantUnitCardDto? currentUnit = null;
        if (lease is not null)
        {
            var today = SaudiTime.Today(_time);

            // Derived from the lease's start day of month rather than read from a schedule:
            // no rent-installment entity exists, and the spec never defines one. Good enough
            // for a monthly lease, wrong for anything else — replace when the schedule lands.
            var dueDate = NextDueDate(lease.StartDate, lease.EndDate, today);

            var lastRentPaidOn = await _db.Payments
                .AsNoTracking()
                .Where(p => p.ContractId == lease.Id && p.Type == PaymentType.Rent
                         && p.Status == PaymentStatus.Paid)
                .OrderByDescending(p => p.Date)
                .Select(p => (DateTimeOffset?)p.Date)
                .FirstOrDefaultAsync(ct);

            // Overdue when the due date has passed and no rent payment has landed since the
            // start of that billing period.
            var periodStart = dueDate.AddMonths(-1);
            var isOverdue = dueDate < today &&
                (lastRentPaidOn is null ||
                 DateOnly.FromDateTime(lastRentPaidOn.Value.UtcDateTime) < periodStart);

            currentUnit = new TenantUnitCardDto
            {
                ContractId = lease.Id,
                PropertyId = lease.PropertyId,
                UnitTitle = lease.PropertyTitle ?? string.Empty,
                InstallmentAmount = lease.MonthlyAmount,
                Currency = lease.Currency,
                DueDate = dueDate,
                IsOverdue = isOverdue
            };
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
                Date = p.Date,
                Type = p.Type,
                InvoiceUrl = p.InvoiceUrl
            })
            .ToListAsync(ct);

        return Result<TenantDashboardDto>.Ok(new TenantDashboardDto
        {
            UserName = user.FullName,
            CurrentUnit = currentUnit,
            // Server-driven so a category can be switched off without shipping a new build.
            MaintenanceCategories = Enum.GetValues<MaintenanceServiceType>(),
            PaymentsPreview = paymentsPreview
        });
    }

    public async Task<Result<BrokerDashboardDto>> GetBrokerAsync(Guid userId, CancellationToken ct)
    {
        var user = await LoadUserAsync(userId, ct);
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
                .Where(m => managed.Any(p => p.Id == m.PropertyId)), ct);

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

        return Result<BrokerDashboardDto>.Ok(new BrokerDashboardDto
        {
            UserName = user.FullName,
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

        static DateOnly BuildDue(int year, int month, int day) =>
            new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
    }

    // Same projection for both dashboards so their maintenance cards cannot drift apart.
    private async Task<List<MaintenanceRequestListItemDto>> MaintenancePreviewAsync(
        IQueryable<Features.Maintenance.Entities.MaintenanceRequest> scoped, CancellationToken ct)
    {
        var rows = await scoped
            .OrderByDescending(m => m.CreatedAt)
            .Take(PreviewCount)
            .Select(m => new
            {
                m.Id,
                m.RequestNumber,
                m.ServiceType,
                m.Status,
                m.CreatedAt,
                m.ScheduledVisitDate,
                m.ScheduledTimeSlot,
                m.PreferredVisitDate,
                m.PreferredTimeSlot,
                Technician = _db.Technicians
                    .Where(t => t.Id == m.AssignedTechnicianId)
                    .Select(t => new
                    {
                        t.Id,
                        t.RoleEn,
                        t.RoleAr,
                        t.RatingAverage,
                        Name = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault(),
                        ImageUrl = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault()
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(m =>
        {
            var date = m.ScheduledVisitDate ?? m.PreferredVisitDate;
            var slot = m.ScheduledTimeSlot ?? m.PreferredTimeSlot;

            return new MaintenanceRequestListItemDto
            {
                Id = m.Id,
                RequestCode = m.RequestNumber,
                Title = MaintenanceServiceTypeNames.Title(m.ServiceType, _language),
                ServiceType = m.ServiceType,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                VisitSummary = $"{date.ToString("d MMMM yyyy", _language.Culture)} · " +
                               $"{MaintenanceTimeSlotNames.Label(slot, _language)}",
                AssignedTechnician = m.Technician is null ? null : new TechnicianDto
                {
                    Id = m.Technician.Id,
                    Name = m.Technician.Name ?? string.Empty,
                    Role = _language.Pick(m.Technician.RoleAr, m.Technician.RoleEn),
                    ImageUrl = m.Technician.ImageUrl,
                    AverageRating = m.Technician.RatingAverage
                }
            };
        }).ToList();
    }

    private Task<BrokerSummaryDto?> BrokerSummaryAsync(Guid brokerId, CancellationToken ct) =>
        _db.Brokers
            .AsNoTracking()
            .Where(b => b.Id == brokerId)
            .Select(b => new BrokerSummaryDto
            {
                Id = b.Id,
                Name = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                LogoUrl = _db.Users.Where(u => u.Id == b.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault(),
                Subtitle = b.SubtitleEn,
                RatingAverage = b.RatingAverage,
                ReviewsCount = b.ReviewsCount,
                IsVerified = b.IsVerified
            })
            .FirstOrDefaultAsync(ct)!;

    private async Task<UserSummary?> LoadUserAsync(Guid userId, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new UserSummary(u.FullName, u.AccountType))
            .FirstOrDefaultAsync(ct);

    private sealed record UserSummary(string? FullName, AccountType AccountType);
}
