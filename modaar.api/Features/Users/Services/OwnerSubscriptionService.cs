using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

public interface IOwnerSubscriptionService
{
    // Broker side: one of their clients' subscription.
    Task<Result<OwnerSubscriptionDto>> GetForBrokerAsync(Guid brokerUserId, Guid ownerUserId, CancellationToken ct);
    Task<Result<OwnerSubscriptionDto>> UpdateForBrokerAsync(
        Guid brokerUserId, Guid ownerUserId, UpdateOwnerSubscriptionDto request, CancellationToken ct);

    // Owner side: read-only.
    Task<Result<OwnerSubscriptionDto>> GetMineAsync(Guid ownerUserId, CancellationToken ct);
}

// The owner's subscription is set by their broker, never by the owner: it is what the two agreed
// in the management agreement. A broker may only touch owners who are their clients — owners
// with at least one property the broker manages.
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

    public async Task<Result<OwnerSubscriptionDto>> GetForBrokerAsync(
        Guid brokerUserId, Guid ownerUserId, CancellationToken ct)
    {
        if (await CheckClientAsync(brokerUserId, ownerUserId, ct) is { } failure)
            return failure;

        return await LoadAsync(ownerUserId, ct);
    }

    public async Task<Result<OwnerSubscriptionDto>> UpdateForBrokerAsync(
        Guid brokerUserId, Guid ownerUserId, UpdateOwnerSubscriptionDto request, CancellationToken ct)
    {
        if (await CheckClientAsync(brokerUserId, ownerUserId, ct) is { } failure)
            return failure;

        var owner = await _db.Users.FirstAsync(u => u.Id == ownerUserId, ct);

        // Requests already raised keep the plan they were raised under; this applies to new ones.
        owner.SubscriptionPlan = request.Plan;
        owner.AutoApproveLimit = request.AutoApproveLimit;
        owner.UpdatedAt = _time.GetUtcNow();

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Owner {OwnerId} subscription set to {Plan} (limit {Limit}) by broker user {BrokerUserId}.",
            ownerUserId, request.Plan, request.AutoApproveLimit, brokerUserId);

        return await LoadAsync(ownerUserId, ct);
    }

    public async Task<Result<OwnerSubscriptionDto>> GetMineAsync(Guid ownerUserId, CancellationToken ct)
    {
        var isOwner = await _db.Users.AnyAsync(u =>
            u.Id == ownerUserId && !u.IsDeleted && u.AccountTypes.Contains(AccountType.Owner), ct);

        if (!isOwner)
            return Result<OwnerSubscriptionDto>.Fail(AppErrorCode.Forbidden, "Only an owner account has a subscription.");

        return await LoadAsync(ownerUserId, ct);
    }

    private async Task<Result<OwnerSubscriptionDto>?> CheckClientAsync(
        Guid brokerUserId, Guid ownerUserId, CancellationToken ct)
    {
        var isClient = await _db.Properties.AnyAsync(p =>
            p.OwnerUserId == ownerUserId && p.BrokerId != null &&
            _db.Brokers.Any(b => b.Id == p.BrokerId && b.UserId == brokerUserId), ct);

        // One answer for "no such owner" and "not your client", so a broker can't probe for users.
        return isClient
            ? null
            : Result<OwnerSubscriptionDto>.Fail(AppErrorCode.NotFound, "Client not found.");
    }

    private async Task<Result<OwnerSubscriptionDto>> LoadAsync(Guid ownerUserId, CancellationToken ct)
    {
        var dto = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerUserId && !u.IsDeleted)
            .Select(u => new OwnerSubscriptionDto
            {
                OwnerUserId = u.Id,
                OwnerName = u.FullName,
                Plan = u.SubscriptionPlan,
                AutoApproveLimit = u.AutoApproveLimit
            })
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result<OwnerSubscriptionDto>.Fail(AppErrorCode.NotFound, "Client not found.")
            : Result<OwnerSubscriptionDto>.Ok(dto);
    }
}
