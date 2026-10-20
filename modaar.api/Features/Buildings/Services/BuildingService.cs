using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Buildings.Dtos;
using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Properties.Dtos;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Properties.Enums;
using modaar.api.Features.Properties.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Buildings.Services;

public interface IBuildingService
{
    Task<Result<List<BuildingListItemDto>>> ListAsync(Guid callerUserId, AccountType role, bool vacantOnly, CancellationToken ct);
    Task<Result<BuildingDetailsDto>> GetAsync(Guid buildingId, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<BuildingDetailsDto>> CreateAsync(Guid callerUserId, AccountType role, CreateBuildingDto request, CancellationToken ct);
    Task<Result<BuildingDetailsDto>> UpdateAsync(Guid buildingId, Guid callerUserId, AccountType role, UpdateBuildingDto request, CancellationToken ct);
    Task<Result<PropertyDetailsDto>> AddUnitAsync(Guid buildingId, Guid callerUserId, AccountType role, AddBuildingUnitDto request, CancellationToken ct);
}

// Buildings for owners and brokers. Because one building can hold several owners' units, the
// caller only ever sees — and is only ever counted over — their own units in it:
//   owner  → units they own
//   broker → units their brokerage manages
public sealed class BuildingService : IBuildingService
{
    private readonly ModaarDbContext _db;
    private readonly IPropertyService _properties;
    private readonly TimeProvider _time;

    public BuildingService(ModaarDbContext db, IPropertyService properties, TimeProvider time)
    {
        _db = db;
        _properties = properties;
        _time = time;
    }

    public async Task<Result<List<BuildingListItemDto>>> ListAsync(
        Guid callerUserId, AccountType role, bool vacantOnly, CancellationToken ct)
    {
        var scope = await ScopeAsync(callerUserId, role, ct);
        if (scope.Failure is { } failure) return Result<List<BuildingListItemDto>>.Fail(failure.Code, failure.Message);

        var buildings = await VisibleBuildings(scope).AsNoTracking().OrderBy(b => b.Name).ToListAsync(ct);
        var stats = await UnitStatsAsync(scope, buildings.Select(b => b.Id).ToList(), ct);

        var items = buildings.Select(b =>
        {
            var units = stats.Where(u => u.BuildingId == b.Id).ToList();
            var rented = units.Count(u => u.Rented);
            return new BuildingListItemDto
            {
                Id = b.Id,
                Name = b.Name,
                Type = b.Type,
                District = b.District,
                City = b.City,
                CoverImageUrl = b.CoverImageUrl,
                UnitsCount = units.Count,
                RentedUnitsCount = rented,
                VacantUnitsCount = units.Count - rented,
                OccupancyPercent = Percent(rented, units.Count),
                MonthlyIncome = units.Sum(u => u.MonthlyRent),
                OverdueCount = units.Count(u => u.Overdue)
            };
        });

        // "شاغرة": buildings with at least one empty unit.
        if (vacantOnly)
            items = items.Where(i => i.VacantUnitsCount > 0);

        return Result<List<BuildingListItemDto>>.Ok(items.ToList());
    }

    public async Task<Result<BuildingDetailsDto>> GetAsync(Guid buildingId, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        var scope = await ScopeAsync(callerUserId, role, ct);
        if (scope.Failure is { } failure) return Result<BuildingDetailsDto>.Fail(failure.Code, failure.Message);

        var building = await VisibleBuildings(scope).AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null)
            return Result<BuildingDetailsDto>.Fail(AppErrorCode.NotFound, "Building not found.");

        var units = await UnitStatsAsync(scope, [buildingId], ct);
        var rented = units.Count(u => u.Rented);

        return Result<BuildingDetailsDto>.Ok(new BuildingDetailsDto
        {
            Id = building.Id,
            Name = building.Name,
            Type = building.Type,
            AddressLine = building.AddressLine,
            District = building.District,
            City = building.City,
            Latitude = building.Latitude,
            Longitude = building.Longitude,
            CoverImageUrl = building.CoverImageUrl,
            CanEdit = building.CreatedByUserId == callerUserId,
            ExpectedMonthlyIncome = units.Sum(u => u.MonthlyRent),
            OccupancyPercent = Percent(rented, units.Count),
            RentedCount = rented,
            VacantCount = units.Count - rented,
            OverdueCount = units.Count(u => u.Overdue),
            Units = units
                .OrderBy(u => u.UnitNumber is null)
                .ThenBy(u => u.UnitNumber?.Length)
                .ThenBy(u => u.UnitNumber)
                .Select(u => new BuildingUnitDto
                {
                    Id = u.Id,
                    UnitNumber = u.UnitNumber,
                    Floor = u.Floor,
                    PropertyType = u.PropertyType,
                    TenantName = u.TenantName,
                    PaymentStatus = !u.Rented ? BuildingUnitPaymentStatus.Vacant
                        : u.Overdue ? BuildingUnitPaymentStatus.Overdue
                        : BuildingUnitPaymentStatus.Paid
                })
                .ToList()
        });
    }

    public async Task<Result<BuildingDetailsDto>> CreateAsync(
        Guid callerUserId, AccountType role, CreateBuildingDto request, CancellationToken ct)
    {
        var scope = await ScopeAsync(callerUserId, role, ct);
        if (scope.Failure is { } failure) return Result<BuildingDetailsDto>.Fail(failure.Code, failure.Message);

        var now = _time.GetUtcNow();
        var building = new Building
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Type = request.Type,
            AddressLine = Clean(request.AddressLine),
            District = Clean(request.District),
            City = Clean(request.City),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            CoverImageUrl = Clean(request.CoverImageUrl),
            CreatedByUserId = callerUserId,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Buildings.Add(building);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(building.Id, callerUserId, role, ct);
    }

    public async Task<Result<BuildingDetailsDto>> UpdateAsync(
        Guid buildingId, Guid callerUserId, AccountType role, UpdateBuildingDto request, CancellationToken ct)
    {
        var building = await _db.Buildings.FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null)
            return Result<BuildingDetailsDto>.Fail(AppErrorCode.NotFound, "Building not found.");

        // Others may have units here too, so only whoever added it renames or moves it.
        if (building.CreatedByUserId != callerUserId)
            return Result<BuildingDetailsDto>.Fail(AppErrorCode.Forbidden, "Only whoever added this building can edit it.");

        if (request.Name is not null) building.Name = request.Name.Trim();
        if (request.Type is { } type) building.Type = type;
        if (request.AddressLine is not null) building.AddressLine = Clean(request.AddressLine);
        if (request.District is not null) building.District = Clean(request.District);
        if (request.City is not null) building.City = Clean(request.City);
        if (request.Latitude is { } lat) building.Latitude = lat;
        if (request.Longitude is { } lng) building.Longitude = lng;
        if (request.CoverImageUrl is not null) building.CoverImageUrl = Clean(request.CoverImageUrl);
        building.UpdatedAt = _time.GetUtcNow();

        await _db.SaveChangesAsync(ct);
        return await GetAsync(building.Id, callerUserId, role, ct);
    }

    public async Task<Result<PropertyDetailsDto>> AddUnitAsync(
        Guid buildingId, Guid callerUserId, AccountType role, AddBuildingUnitDto request, CancellationToken ct)
    {
        var scope = await ScopeAsync(callerUserId, role, ct);
        if (scope.Failure is { } failure) return Result<PropertyDetailsDto>.Fail(failure.Code, failure.Message);

        var building = await VisibleBuildings(scope).AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct);
        if (building is null)
            return Result<PropertyDetailsDto>.Fail(AppErrorCode.NotFound, "Building not found.");

        Guid ownerUserId;
        if (scope.BrokerId is { } brokerId)
        {
            // A broker adds units for owners who are already their clients — the management
            // relationship that lets them run the unit already exists.
            if (request.OwnerUserId is not { } owner)
                return Result<PropertyDetailsDto>.Fail(AppErrorCode.ValidationFailed, "ownerUserId is required: which of your clients owns this unit.");

            var isClient = await _db.Properties.AnyAsync(p => p.OwnerUserId == owner && p.BrokerId == brokerId && !p.IsDeleted, ct);
            if (!isClient)
                return Result<PropertyDetailsDto>.Fail(AppErrorCode.NotFound, "Client not found.");

            ownerUserId = owner;
        }
        else
        {
            if (request.OwnerUserId is { } other && other != callerUserId)
                return Result<PropertyDetailsDto>.Fail(AppErrorCode.Forbidden, "An owner can only add their own units.");

            ownerUserId = callerUserId;
        }

        var now = _time.GetUtcNow();
        var unit = new Property
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            BrokerId = scope.BrokerId,
            BuildingId = building.Id,
            UnitNumber = Clean(request.UnitNumber),
            Floor = Clean(request.Floor),
            Title = building.Name,
            PropertyType = request.PropertyType,
            RoomsCount = request.RoomsCount,
            Area = request.Area,
            BathroomsCount = request.BathroomsCount,
            AnnualRent = request.AnnualRent,
            AddressLine = building.AddressLine,
            District = building.District,
            City = building.City,
            Latitude = building.Latitude,
            Longitude = building.Longitude,
            CoverImageUrl = building.CoverImageUrl,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Properties.Add(unit);
        await _db.SaveChangesAsync(ct);

        return await _properties.GetDetailsAsync(unit.Id, callerUserId, role, ct);
    }

    // ---- helpers ----

    private sealed record Scope(Guid UserId, Guid? BrokerId, Failure? Failure);
    private sealed record Failure(AppErrorCode Code, string Message);

    private async Task<Scope> ScopeAsync(Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (role == AccountType.Owner)
            return new Scope(callerUserId, null, null);

        if (role == AccountType.Broker)
        {
            var brokerId = await _db.Brokers.Where(b => b.UserId == callerUserId).Select(b => (Guid?)b.Id).FirstOrDefaultAsync(ct);
            return brokerId is null
                ? new Scope(callerUserId, null, new Failure(AppErrorCode.Forbidden, "No broker profile for this account."))
                : new Scope(callerUserId, brokerId, null);
        }

        return new Scope(callerUserId, null, new Failure(AppErrorCode.Forbidden, "Buildings are for owners and brokers."));
    }

    // The caller's units: owned (owner) or managed (broker).
    private IQueryable<Property> MyUnits(Scope scope) => scope.BrokerId is { } brokerId
        ? _db.Properties.Where(p => !p.IsDeleted && p.BrokerId == brokerId)
        : _db.Properties.Where(p => !p.IsDeleted && p.OwnerUserId == scope.UserId);

    // Buildings holding one of the caller's units, or added by them (possibly still empty).
    private IQueryable<Building> VisibleBuildings(Scope scope)
    {
        var mine = MyUnits(scope);
        return _db.Buildings.Where(b => b.CreatedByUserId == scope.UserId || mine.Any(p => p.BuildingId == b.Id));
    }

    private sealed record UnitStat(
        Guid Id, Guid? BuildingId, string? UnitNumber, string? Floor, PropertyType PropertyType,
        bool Rented, string? TenantName, decimal MonthlyRent, bool Overdue);

    private async Task<List<UnitStat>> UnitStatsAsync(Scope scope, List<Guid> buildingIds, CancellationToken ct)
    {
        var today = SaudiTime.Today(_time);

        var rows = await MyUnits(scope)
            .AsNoTracking()
            .Where(p => p.BuildingId != null && buildingIds.Contains(p.BuildingId.Value))
            .Select(p => new
            {
                p.Id,
                p.BuildingId,
                p.UnitNumber,
                p.Floor,
                p.PropertyType,
                Lease = _db.Contracts
                    .Where(c => c.PropertyId == p.Id && c.Status == ContractStatus.Active)
                    .Select(c => new
                    {
                        c.InstallmentAmount,
                        c.PaymentFrequency,
                        TenantName = _db.Users.Where(u => u.Id == c.TenantUserId).Select(u => u.FullName).FirstOrDefault()
                    })
                    .FirstOrDefault(),
                Overdue = _db.Payments.Any(pay => pay.PropertyId == p.Id && pay.Type == PaymentType.Rent
                    && pay.Status == PaymentStatus.Pending && pay.DueDate != null && pay.DueDate < today)
            })
            .ToListAsync(ct);

        return rows.Select(r => new UnitStat(
            r.Id, r.BuildingId, r.UnitNumber, r.Floor, r.PropertyType,
            r.Lease is not null, r.Lease?.TenantName,
            r.Lease is null ? 0 : Monthly(r.Lease.InstallmentAmount, r.Lease.PaymentFrequency),
            r.Lease is not null && r.Overdue)).ToList();
    }

    private static decimal Monthly(decimal installment, PaymentFrequency frequency) => frequency switch
    {
        PaymentFrequency.Quarterly => Math.Round(installment / 3, 2),
        PaymentFrequency.SemiAnnual => Math.Round(installment / 6, 2),
        PaymentFrequency.Annual => Math.Round(installment / 12, 2),
        _ => installment
    };

    private static int Percent(int part, int whole) => whole == 0 ? 0 : (int)Math.Round(100m * part / whole);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
