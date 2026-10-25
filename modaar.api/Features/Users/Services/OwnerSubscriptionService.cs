using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

public interface IOwnerSubscriptionService
{
    // Broker side: one of their clients, building by building.
    Task<Result<OwnerSubscriptionDto>> GetForBrokerAsync(Guid brokerUserId, Guid ownerUserId, CancellationToken ct);

    // Every building of the client's the broker runs units in.
    Task<Result<OwnerSubscriptionDto>> UpdateForBrokerAsync(
        Guid brokerUserId, Guid ownerUserId, UpdateOwnerSubscriptionDto request, CancellationToken ct);

    // One building.
    Task<Result<BuildingSubscriptionDto>> UpdateBuildingForBrokerAsync(
        Guid brokerUserId, Guid ownerUserId, Guid buildingId, UpdateOwnerSubscriptionDto request, CancellationToken ct);

    // Owner side: read-only.
    Task<Result<OwnerSubscriptionDto>> GetMineAsync(Guid ownerUserId, CancellationToken ct);
}

// The plan belongs to an owner in a building (BuildingOwnerSubscription). It is what the owner and
// their broker agreed, so the broker sets it and the owner only reads it. A broker reaches a
// building of an owner's only where they run at least one of that owner's units.
public sealed class OwnerSubscriptionService : IOwnerSubscriptionService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<OwnerSubscriptionService> _logger;

    public OwnerSubscriptionService(ModaarDbContext db, TimeProvider time, ILogger<OwnerSubscriptionService> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task<Result<OwnerSubscriptionDto>> GetForBrokerAsync(Guid brokerUserId, Guid ownerUserId, CancellationToken ct)
    {
        var buildings = await BrokerBuildingsAsync(brokerUserId, ownerUserId, ct);

        // One answer for "no such owner" and "not your client", so a broker can't probe for users.
        return buildings.Count == 0
            ? Result<OwnerSubscriptionDto>.Fail(AppErrorCode.NotFound, "Client not found.")
            : Result<OwnerSubscriptionDto>.Ok(await LoadAsync(ownerUserId, buildings, ct));
    }

    public async Task<Result<OwnerSubscriptionDto>> UpdateForBrokerAsync(
        Guid brokerUserId, Guid ownerUserId, UpdateOwnerSubscriptionDto request, CancellationToken ct)
    {
        var buildings = await BrokerBuildingsAsync(brokerUserId, ownerUserId, ct);
        if (buildings.Count == 0)
            return Result<OwnerSubscriptionDto>.Fail(AppErrorCode.NotFound, "Client not found.");

        await SetAsync(ownerUserId, buildings, request, brokerUserId, ct);
        return Result<OwnerSubscriptionDto>.Ok(await LoadAsync(ownerUserId, buildings, ct));
    }

    public async Task<Result<BuildingSubscriptionDto>> UpdateBuildingForBrokerAsync(
        Guid brokerUserId, Guid ownerUserId, Guid buildingId, UpdateOwnerSubscriptionDto request, CancellationToken ct)
    {
        var buildings = await BrokerBuildingsAsync(brokerUserId, ownerUserId, ct);
        if (!buildings.Contains(buildingId))
            return Result<BuildingSubscriptionDto>.Fail(AppErrorCode.NotFound, "Client building not found.");

        await SetAsync(ownerUserId, [buildingId], request, brokerUserId, ct);
        return Result<BuildingSubscriptionDto>.Ok((await LoadAsync(ownerUserId, [buildingId], ct)).Buildings.Single());
    }

    public async Task<Result<OwnerSubscriptionDto>> GetMineAsync(Guid ownerUserId, CancellationToken ct)
    {
        var isOwner = await _db.Users.AnyAsync(u =>
            u.Id == ownerUserId && !u.IsDeleted && u.AccountTypes.Contains(AccountType.Owner), ct);

        if (!isOwner)
            return Result<OwnerSubscriptionDto>.Fail(AppErrorCode.Forbidden, "Only an owner account has a subscription.");

        var buildings = await _db.Properties
            .Where(p => p.OwnerUserId == ownerUserId && !p.IsDeleted && p.BuildingId != null)
            .Select(p => p.BuildingId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return Result<OwnerSubscriptionDto>.Ok(await LoadAsync(ownerUserId, buildings, ct));
    }

    // Buildings holding at least one unit of this owner's that this broker runs.
    private Task<List<Guid>> BrokerBuildingsAsync(Guid brokerUserId, Guid ownerUserId, CancellationToken ct) =>
        _db.Properties
            .Where(p => p.OwnerUserId == ownerUserId && !p.IsDeleted && p.BuildingId != null && p.BrokerId != null
                        && _db.Brokers.Any(b => b.Id == p.BrokerId && b.UserId == brokerUserId))
            .Select(p => p.BuildingId!.Value)
            .Distinct()
            .ToListAsync(ct);

    // Requests already raised keep the plan they were raised under; this applies to new ones.
    private async Task SetAsync(Guid ownerUserId, IReadOnlyCollection<Guid> buildingIds,
        UpdateOwnerSubscriptionDto request, Guid actorUserId, CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var existing = await _db.BuildingOwnerSubscriptions
            .Where(s => s.OwnerUserId == ownerUserId && buildingIds.Contains(s.BuildingId))
            .ToDictionaryAsync(s => s.BuildingId, ct);

        foreach (var buildingId in buildingIds)
        {
            if (!existing.TryGetValue(buildingId, out var row))
            {
                row = new BuildingOwnerSubscription { Id = Guid.NewGuid(), BuildingId = buildingId, OwnerUserId = ownerUserId, CreatedAt = now };
                _db.BuildingOwnerSubscriptions.Add(row);
            }

            row.Plan = request.Plan;
            row.AutoApproveLimit = request.AutoApproveLimit;
            row.UpdatedByUserId = actorUserId;
            row.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Owner {OwnerId} subscription set to {Plan} (limit {Limit}) in {Count} building(s) by user {UserId}.",
            ownerUserId, request.Plan, request.AutoApproveLimit, buildingIds.Count, actorUserId);
    }

    private async Task<OwnerSubscriptionDto> LoadAsync(Guid ownerUserId, IReadOnlyCollection<Guid> buildingIds, CancellationToken ct)
    {
        var name = await _db.Users.Where(u => u.Id == ownerUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

        var rows = await _db.Buildings
            .AsNoTracking()
            .Where(b => buildingIds.Contains(b.Id))
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.City,
                Units = _db.Properties.Count(p => p.BuildingId == b.Id && p.OwnerUserId == ownerUserId && !p.IsDeleted),
                Sub = _db.BuildingOwnerSubscriptions
                    .Where(s => s.BuildingId == b.Id && s.OwnerUserId == ownerUserId)
                    .Select(s => new { s.Plan, s.AutoApproveLimit })
                    .FirstOrDefault()
            })
            .OrderBy(b => b.Name)
            .ToListAsync(ct);

        return new OwnerSubscriptionDto
        {
            OwnerUserId = ownerUserId,
            OwnerName = name,
            Buildings = rows.Select(b => new BuildingSubscriptionDto
            {
                BuildingId = b.Id,
                BuildingName = b.Name,
                City = b.City,
                UnitsCount = b.Units,
                Plan = b.Sub?.Plan ?? BuildingSubscriptions.Default.Plan,
                AutoApproveLimit = b.Sub?.AutoApproveLimit,
                IsDefault = b.Sub is null
            }).ToList()
        };
    }
}
