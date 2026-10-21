using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

public interface IBrokerClientService
{
    Task<Result<PagedResultDto<BrokerClientListItemDto>>> ListAsync(
        Guid callerUserId, AccountType activeRole, BrokerClientsRequestDto request, CancellationToken ct);

    Task<Result<BrokerClientDetailsDto>> GetAsync(
        Guid callerUserId, AccountType activeRole, Guid ownerUserId, CancellationToken ct);
}

// A client is an owner with at least one unit this broker manages — the same rule
// OwnerSubscriptionService uses to let the broker set the owner's plan. Everything shown is over
// the broker's units only.
public sealed class BrokerClientService : IBrokerClientService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;

    public BrokerClientService(ModaarDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    private sealed record Unit(Guid Id, Guid OwnerUserId, Guid? BuildingId, string? BuildingName,
        string Title, string? City, string? District, bool Rented, decimal MonthlyRent);

    public async Task<Result<PagedResultDto<BrokerClientListItemDto>>> ListAsync(
        Guid callerUserId, AccountType activeRole, BrokerClientsRequestDto request, CancellationToken ct)
    {
        var (isBroker, broker) = await BrokerAsync(callerUserId, activeRole, ct);
        if (!isBroker)
            return Result<PagedResultDto<BrokerClientListItemDto>>.Fail(AppErrorCode.Forbidden, "Only a broker has clients.");

        if (broker is not { } brokerId)
            return Result<PagedResultDto<BrokerClientListItemDto>>.Ok(PagedResultDto<BrokerClientListItemDto>.Empty);

        // A broker's portfolio is small enough to group in memory, which keeps "main city" and
        // the building count simple.
        var units = await UnitsAsync(brokerId, ownerUserId: null, ct);
        var ownerIds = units.Select(u => u.OwnerUserId).Distinct().ToList();

        var owners = _db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id) && !u.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            owners = owners.Where(u => (u.FullName != null && u.FullName.Contains(term))
                                       || (u.CountryCode + u.PhoneNumber).Contains(term));
        }

        var people = await owners
            .Select(u => new { u.Id, u.FullName, Phone = u.CountryCode + u.PhoneNumber, u.ProfileImageUrl, u.SubscriptionPlan, u.AutoApproveLimit })
            .ToListAsync(ct);

        var rows = people
            .Select(u =>
            {
                var mine = units.Where(x => x.OwnerUserId == u.Id).ToList();
                var rented = mine.Count(x => x.Rented);
                return new BrokerClientListItemDto
                {
                    OwnerUserId = u.Id,
                    Name = u.FullName,
                    PhoneNumber = u.Phone,
                    ImageUrl = u.ProfileImageUrl,
                    City = mine.Where(x => x.City != null).GroupBy(x => x.City).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault(),
                    PropertiesCount = mine.Select(x => x.BuildingId ?? x.Id).Distinct().Count(),
                    TotalUnits = mine.Count,
                    RentedUnits = rented,
                    VacantUnits = mine.Count - rented,
                    MonthlyIncome = Math.Round(mine.Sum(x => x.MonthlyRent), 2),
                    Plan = u.SubscriptionPlan,
                    AutoApproveLimit = u.AutoApproveLimit
                };
            })
            .OrderByDescending(r => r.TotalUnits)
            .ThenBy(r => r.Name)
            .ToList();

        return Result<PagedResultDto<BrokerClientListItemDto>>.Ok(new PagedResultDto<BrokerClientListItemDto>
        {
            Items = rows.Skip(request.SkipCount).Take(request.MaxResultCount).ToList(),
            TotalCount = rows.Count
        });
    }

    public async Task<Result<BrokerClientDetailsDto>> GetAsync(
        Guid callerUserId, AccountType activeRole, Guid ownerUserId, CancellationToken ct)
    {
        var (isBroker, broker) = await BrokerAsync(callerUserId, activeRole, ct);
        if (!isBroker)
            return Result<BrokerClientDetailsDto>.Fail(AppErrorCode.Forbidden, "Only a broker has clients.");

        var units = broker is { } brokerId ? await UnitsAsync(brokerId, ownerUserId, ct) : [];

        var owner = units.Count == 0 ? null : await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerUserId && !u.IsDeleted)
            .Select(u => new { u.FullName, Phone = u.CountryCode + u.PhoneNumber, u.ProfileImageUrl, u.SubscriptionPlan, u.AutoApproveLimit })
            .FirstOrDefaultAsync(ct);

        // One answer for "no such owner" and "not your client", so a broker can't probe for users.
        if (owner is null)
            return Result<BrokerClientDetailsDto>.Fail(AppErrorCode.NotFound, "Client not found.");

        var ownerSince = await _db.BrokerManagementRequests
            .AsNoTracking()
            .Where(r => r.BrokerId == broker && r.OwnerUserId == ownerUserId && r.Status == BrokerManagementRequestStatus.Accepted)
            .MinAsync(r => r.DecidedAt, ct);

        var unitIds = units.Select(u => u.Id).ToList();

        var today = SaudiTime.Today(_time);
        var first = new DateOnly(today.Year, today.Month, 1);
        var next = first.AddMonths(1);
        var start = first.ToDateTime(TimeOnly.MinValue);
        var from = new DateTimeOffset(start, SaudiTime.Zone.GetUtcOffset(start));
        var to = from.AddMonths(1);

        // Same month rules as the dashboards.
        var payments = _db.Payments.AsNoTracking().Where(p => p.PropertyId != null && unitIds.Contains(p.PropertyId.Value));
        var collected = await payments
            .Where(p => p.Status == PaymentStatus.Paid && p.Date >= from && p.Date < to)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var expected = await payments
            .Where(p => p.Type == PaymentType.Rent && p.DueDate != null && p.DueDate >= first && p.DueDate < next)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var openMaintenance = await _db.MaintenanceRequests
            .AsNoTracking()
            .CountAsync(m => unitIds.Contains(m.PropertyId)
                             && (m.Status == MaintenanceRequestStatus.Pending || m.Status == MaintenanceRequestStatus.Accepted), ct);

        var rented = units.Count(u => u.Rented);

        var properties = units
            .GroupBy(u => u.BuildingId ?? u.Id)
            .Select(g =>
            {
                var any = g.First();
                var r = g.Count(u => u.Rented);
                return new BrokerClientPropertyDto
                {
                    Id = g.Key,
                    Name = any.BuildingName ?? any.Title,
                    City = any.City,
                    District = any.District,
                    TotalUnits = g.Count(),
                    RentedUnits = r,
                    OccupancyPercent = Percent(r, g.Count())
                };
            })
            .OrderByDescending(p => p.TotalUnits)
            .ThenBy(p => p.Name)
            .ToList();

        return Result<BrokerClientDetailsDto>.Ok(new BrokerClientDetailsDto
        {
            OwnerUserId = ownerUserId,
            Name = owner.FullName,
            PhoneNumber = owner.Phone,
            ImageUrl = owner.ProfileImageUrl,
            OwnerSince = ownerSince,
            Plan = owner.SubscriptionPlan,
            AutoApproveLimit = owner.AutoApproveLimit,
            TotalUnits = units.Count,
            RentedUnits = rented,
            VacantUnits = units.Count - rented,
            OccupancyPercent = Percent(rented, units.Count),
            MonthlyIncome = Math.Round(units.Sum(u => u.MonthlyRent), 2),
            CollectedThisMonth = collected,
            ExpectedThisMonth = expected,
            OpenMaintenanceCount = openMaintenance,
            Properties = properties
        });
    }

    // IsBroker false: the caller isn't acting as a broker. BrokerId null: a broker with no Brokers
    // row, so no clients.
    private async Task<(bool IsBroker, Guid? BrokerId)> BrokerAsync(Guid callerUserId, AccountType activeRole, CancellationToken ct)
    {
        if (activeRole != AccountType.Broker)
            return (false, null);

        var id = await _db.Brokers
            .Where(b => b.UserId == callerUserId)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

        return (true, id);
    }

    private Task<List<Unit>> UnitsAsync(Guid brokerId, Guid? ownerUserId, CancellationToken ct) =>
        _db.Properties
            .AsNoTracking()
            .Where(p => p.BrokerId == brokerId && !p.IsDeleted && (ownerUserId == null || p.OwnerUserId == ownerUserId))
            .Select(p => new Unit(
                p.Id,
                p.OwnerUserId,
                p.BuildingId,
                _db.Buildings.Where(b => b.Id == p.BuildingId).Select(b => b.Name).FirstOrDefault(),
                p.Title,
                p.City,
                p.District,
                // Rented = has an active contract, as everywhere else (PropertyService, BuildingService).
                _db.Contracts.Any(c => c.PropertyId == p.Id && c.Status == ContractStatus.Active),
                _db.Contracts
                    .Where(c => c.PropertyId == p.Id && c.Status == ContractStatus.Active)
                    .Sum(c => (decimal?)(c.AnnualRent != null
                        ? c.AnnualRent.Value / 12m
                        : c.PaymentFrequency == PaymentFrequency.Quarterly ? c.InstallmentAmount / 3m
                        : c.PaymentFrequency == PaymentFrequency.SemiAnnual ? c.InstallmentAmount / 6m
                        : c.PaymentFrequency == PaymentFrequency.Annual ? c.InstallmentAmount / 12m
                        : c.InstallmentAmount)) ?? 0m))
            .ToListAsync(ct);

    private static int Percent(int part, int whole) =>
        whole == 0 ? 0 : (int)Math.Round(part * 100m / whole, MidpointRounding.AwayFromZero);
}
