using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Agreements.Dtos;
using modaar.api.Features.Agreements.Entities;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Notifications.Services;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Agreements.Services;

public interface IManagementAgreementService
{
    // Owner sends a mandate to a broker.
    Task<Result<AgreementDetailsDto>> CreateAsync(
        Guid callerUserId, AccountType role, CreateAgreementDto request, CancellationToken ct);

    // Both sides.
    Task<Result<PagedResultDto<AgreementListItemDto>>> ListAsync(
        Guid callerUserId, AccountType role, AgreementsRequestDto query, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> GetAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct);

    // Broker's answer.
    Task<Result<AgreementDetailsDto>> AcceptAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> RejectAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct);

    Task<Result<AgreementDetailsDto>> CancelAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> RenewAsync(Guid id, Guid callerUserId, AccountType role, RenewAgreementDto request, CancellationToken ct);

    // Re-reads the leases of these units and lets any whose lease is over leave. Called by the
    // daily job and right after a lease is ended early.
    Task SyncPropertiesAsync(IReadOnlyCollection<Guid>? propertyIds, CancellationToken ct);
}

// The rules, in one place:
// - The owner sends it (units + maintenance auto-approve limit) → pendingBroker. The broker
//   accepts → active, or rejects. No fees or other terms. Brokers can't start one.
// - On accepting, each unit is "held": Property.BrokerId points at the broker, and the unit
//   leaves when its lease does — the current lease, or the first one signed on it if it was
//   vacant. A lease ended early → it leaves that day. A next lease made by the same broker keeps it.
// - When the last unit has left the agreement is expired. Either side may cancel an active one
//   with a reason; every unit leaves that day. The owner may withdraw a pending one.
// - Leaving does NOT clear Property.BrokerId: the broker keeps running the unit after the
//   mandate ends (user's decision, 2026-11-02), just without the limit (MandateLimits).
public sealed class ManagementAgreementService : IManagementAgreementService
{
    public const int EndingSoonDays = 30;

    private readonly ModaarDbContext _db;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly ILogger<ManagementAgreementService> _logger;

    public ManagementAgreementService(
        ModaarDbContext db, INotificationService notifications, TimeProvider time, ILogger<ManagementAgreementService> logger)
    {
        _db = db;
        _notifications = notifications;
        _time = time;
        _logger = logger;
    }

    // ── Owner sends ────────────────────────────────────────────────────────

    public async Task<Result<AgreementDetailsDto>> CreateAsync(
        Guid callerUserId, AccountType role, CreateAgreementDto request, CancellationToken ct)
    {
        if (role != AccountType.Owner)
            return Fail(AppErrorCode.Forbidden, "Only an owner sends a mandate.");

        // The token is signed but can be stale: a role removed since selection doesn't count.
        var isOwner = await _db.Users.AnyAsync(u =>
            u.Id == callerUserId && !u.IsDeleted && u.IsActive && u.AccountTypes.Contains(AccountType.Owner), ct);
        if (!isOwner)
            return Fail(AppErrorCode.Forbidden, "That role is no longer available on this account.");

        var broker = await _db.Brokers.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BrokerId, ct);
        if (broker is null)
            return Fail(AppErrorCode.NotFound, "Broker not found.");

        var alreadyPending = await _db.ManagementAgreements.AnyAsync(a =>
            a.OwnerUserId == callerUserId && a.BrokerId == broker.Id && a.Status == ManagementAgreementStatus.PendingBroker, ct);
        if (alreadyPending)
            return Fail(AppErrorCode.Conflict, "You already have a mandate waiting for this broker.");

        return await SendAsync(broker, callerUserId, request.PropertyIds?.Distinct().ToList() ?? [], strictUnits: true,
            request.AutoApproveLimit, renewedFrom: null, ct);
    }

    // ── Reading ────────────────────────────────────────────────────────────

    public async Task<Result<PagedResultDto<AgreementListItemDto>>> ListAsync(
        Guid callerUserId, AccountType role, AgreementsRequestDto query, CancellationToken ct)
    {
        IQueryable<ManagementAgreement> rows;
        if (role == AccountType.Owner)
            rows = _db.ManagementAgreements.Where(a => a.OwnerUserId == callerUserId);
        else if (await BrokerOfAsync(callerUserId, role, ct) is { } broker)
            rows = _db.ManagementAgreements.Where(a => a.BrokerId == broker.Id);
        else
            return Result<PagedResultDto<AgreementListItemDto>>.Fail(AppErrorCode.Forbidden, "Agreements are between owners and brokers.");

        if (query.Status is { } status)
            rows = rows.Where(a => a.Status == status);

        var total = await rows.CountAsync(ct);
        var page = await rows
            .AsNoTracking()
            .OrderBy(a => a.Status == ManagementAgreementStatus.PendingBroker ? 0 : a.Status == ManagementAgreementStatus.Active ? 1 : 2)
            .ThenByDescending(a => a.CreatedAt)
            .Skip(query.SkipCount).Take(query.MaxResultCount)
            .Include(a => a.Properties)
            .ToListAsync(ct);

        var brokerUsers = role == AccountType.Owner
            ? await _db.Brokers.Where(b => page.Select(a => a.BrokerId).Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.UserId, ct)
            : [];
        var partyIds = page.Select(a => role == AccountType.Owner ? brokerUsers[a.BrokerId] : a.OwnerUserId).Distinct().ToList();
        var parties = await PartiesAsync(partyIds, ct);

        var today = SaudiTime.Today(_time);
        return Result<PagedResultDto<AgreementListItemDto>>.Ok(new PagedResultDto<AgreementListItemDto>
        {
            TotalCount = total,
            Items = page.Select(a =>
            {
                var end = EndDate(a);
                return new AgreementListItemDto
                {
                    Id = a.Id,
                    Number = a.Number,
                    Status = a.Status,
                    DisplayStatus = Display(a, end, today),
                    OtherParty = parties[role == AccountType.Owner ? brokerUsers[a.BrokerId] : a.OwnerUserId],
                    UnitsCount = a.Status == ManagementAgreementStatus.PendingBroker
                        ? a.Properties.Count
                        : a.Properties.Count(p => p.ActiveFrom != null && p.ExitedOn == null),
                    AutoApproveLimit = a.AutoApproveLimit,
                    StartDate = a.StartDate,
                    EndDate = end,
                    DaysLeft = DaysLeft(a, end, today),
                    IsRenewal = a.RenewedFromId != null,
                    CreatedAt = a.CreatedAt
                };
            }).ToList()
        });
    }

    public async Task<Result<AgreementDetailsDto>> GetAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { } access)
            return Fail(AppErrorCode.NotFound, "Agreement not found.");

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(access.Agreement.Id, access.IsOwner, ct));
    }

    // ── Broker's answer ────────────────────────────────────────────────────

    public async Task<Result<AgreementDetailsDto>> AcceptAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { IsOwner: false } access)
            return await NotYoursAsync(id, callerUserId, role, "Only the broker accepts a mandate.", ct);

        var agreement = access.Agreement;
        if (agreement.Status != ManagementAgreementStatus.PendingBroker)
            return Fail(AppErrorCode.Conflict, "This mandate isn't waiting for an answer.");

        var today = SaudiTime.Today(_time);
        var now = _time.GetUtcNow();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // A renewal takes over the units the old agreement still holds: release them first so the
        // one-holder index never sees two.
        if (agreement.RenewedFromId is { } oldId)
        {
            var old = await _db.ManagementAgreements.Include(a => a.Properties).FirstAsync(a => a.Id == oldId, ct);
            foreach (var unit in old.Properties.Where(p => p.ActiveFrom != null && p.ExitedOn == null))
            {
                unit.ExitedOn = today;
                unit.ExitReason = AgreementUnitExitReason.Renewed;
            }
            if (old.Status == ManagementAgreementStatus.Active)
            {
                old.Status = ManagementAgreementStatus.Expired;
                old.EndedOn = today;
                old.EndReason = $"Renewed by {agreement.Number}.";
                old.UpdatedAt = now;
                AddEvent(old.Id, AgreementEventType.Renewed, callerUserId, note: agreement.Number);
            }
            await _db.SaveChangesAsync(ct);
        }

        var propertyIds = agreement.Properties.Select(p => p.PropertyId).ToList();
        var properties = await _db.Properties
            .Include(p => p.Contracts)
            .Where(p => propertyIds.Contains(p.Id))
            .AsSplitQuery()
            .ToDictionaryAsync(p => p.Id, ct);

        // Units no longer the owner's drop out.
        foreach (var unit in agreement.Properties.ToList())
        {
            if (!properties.TryGetValue(unit.PropertyId, out var p) || p.IsDeleted || p.OwnerUserId != agreement.OwnerUserId)
                agreement.Properties.Remove(unit);
        }

        // A unit is in one active mandate at a time: the owner cancels the other one first. A unit
        // another broker runs outside any mandate moves to this broker: the owner chose so.
        var heldElsewhere = await _db.AgreementProperties
            .Where(x => propertyIds.Contains(x.PropertyId) && x.ActiveFrom != null && x.ExitedOn == null && x.AgreementId != agreement.Id)
            .Select(x => x.PropertyId)
            .ToListAsync(ct);

        if (heldElsewhere.Count > 0)
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return Fail(AppErrorCode.Conflict,
                $"{heldElsewhere.Count} unit(s) in this mandate are in another active mandate: {string.Join(", ", heldElsewhere)}.");
        }

        if (agreement.Properties.Count == 0)
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return Fail(AppErrorCode.Conflict, "None of the units in this mandate can be managed any more.");
        }

        foreach (var unit in agreement.Properties)
        {
            unit.ActiveFrom = today;
            var lease = Evaluate(properties[unit.PropertyId].Contracts, contractId: null, agreement.BrokerId, today);
            unit.ContractId = lease.ContractId;
            unit.EndsOn = lease.EndsOn;
            properties[unit.PropertyId].BrokerId = agreement.BrokerId;
            properties[unit.PropertyId].UpdatedAt = now;
        }

        agreement.Status = ManagementAgreementStatus.Active;
        agreement.StartDate = today;
        agreement.ApprovedAt = now;
        agreement.UpdatedAt = now;
        AddEvent(agreement.Id, AgreementEventType.Accepted, callerUserId);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Agreement {Number} accepted: broker {BrokerId} now runs {Count} unit(s) of owner {OwnerId}.",
            agreement.Number, agreement.BrokerId, agreement.Properties.Count, agreement.OwnerUserId);

        await _notifications.NotifyAsync([agreement.OwnerUserId],
            AgreementNotifications.Accepted(agreement.Id, agreement.Number, await NameAsync(callerUserId, ct)), ct);

        // A unit whose lease is already over leaves straight away.
        await SyncPropertiesAsync(agreement.Properties.Select(p => p.PropertyId).ToList(), ct);

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, forOwner: false, ct));
    }

    public async Task<Result<AgreementDetailsDto>> RejectAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { IsOwner: false } access)
            return await NotYoursAsync(id, callerUserId, role, "Only the broker rejects a mandate.", ct);

        var agreement = access.Agreement;
        if (agreement.Status != ManagementAgreementStatus.PendingBroker)
            return Fail(AppErrorCode.Conflict, "This mandate isn't waiting for an answer.");

        End(agreement, ManagementAgreementStatus.Rejected, callerUserId, reason);
        AddEvent(agreement.Id, AgreementEventType.Rejected, callerUserId, note: reason.Trim());
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync([agreement.OwnerUserId],
            AgreementNotifications.Rejected(agreement.Id, agreement.Number, await NameAsync(callerUserId, ct)), ct);

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, forOwner: false, ct));
    }

    // ── Either side ────────────────────────────────────────────────────────

    // Owner on a pending one → withdrawn. Either side on an active one → terminated, every unit
    // leaves today.
    public async Task<Result<AgreementDetailsDto>> CancelAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { } access)
            return Fail(AppErrorCode.NotFound, "Agreement not found.");

        var agreement = access.Agreement;
        var brokerUserId = await BrokerUserAsync(agreement.BrokerId, ct);
        var callerName = await NameAsync(callerUserId, ct);

        if (agreement.Status == ManagementAgreementStatus.PendingBroker)
        {
            if (!access.IsOwner)
                return Fail(AppErrorCode.Conflict, "To turn down a pending mandate, reject it.");

            End(agreement, ManagementAgreementStatus.Withdrawn, callerUserId, reason);
            AddEvent(agreement.Id, AgreementEventType.Withdrawn, callerUserId, note: reason.Trim());
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyAsync([brokerUserId],
                AgreementNotifications.Withdrawn(agreement.Id, agreement.Number, callerName), ct);
            return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, access.IsOwner, ct));
        }

        if (agreement.Status != ManagementAgreementStatus.Active)
            return Fail(AppErrorCode.Conflict, "This agreement has already ended.");

        var today = SaudiTime.Today(_time);
        foreach (var unit in agreement.Properties.Where(p => p.ActiveFrom != null && p.ExitedOn == null))
            Exit(agreement, unit, AgreementUnitExitReason.AgreementTerminated, today);

        End(agreement, ManagementAgreementStatus.Terminated, callerUserId, reason);
        AddEvent(agreement.Id, AgreementEventType.Terminated, callerUserId, note: reason.Trim());

        // A renewal waiting on this one has nothing left to renew.
        var pendingRenewals = await _db.ManagementAgreements
            .Where(a => a.RenewedFromId == agreement.Id && a.Status == ManagementAgreementStatus.PendingBroker)
            .ToListAsync(ct);
        foreach (var renewal in pendingRenewals)
        {
            End(renewal, ManagementAgreementStatus.Withdrawn, callerUserId, $"{agreement.Number} was cancelled.");
            AddEvent(renewal.Id, AgreementEventType.Withdrawn, callerUserId, note: $"{agreement.Number} was cancelled.");
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Agreement {Number} cancelled by user {UserId}.", agreement.Number, callerUserId);

        var other = access.IsOwner ? brokerUserId : agreement.OwnerUserId;
        await _notifications.NotifyAsync([other], AgreementNotifications.Terminated(agreement.Id, agreement.Number, callerName), ct);

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, access.IsOwner, ct));
    }

    // Owner carries on with the same broker: a new pending mandate, units and limit copied unless
    // given. Works on an active agreement and on one that expired (its units may come back).
    public async Task<Result<AgreementDetailsDto>> RenewAsync(Guid id, Guid callerUserId, AccountType role, RenewAgreementDto request, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { IsOwner: true } access)
            return await NotYoursAsync(id, callerUserId, role, "Only the owner renews a mandate.", ct);

        var agreement = access.Agreement;
        if (agreement.Status is not (ManagementAgreementStatus.Active or ManagementAgreementStatus.Expired))
            return Fail(AppErrorCode.Conflict, "Only an active or expired agreement can be renewed.");

        if (await _db.ManagementAgreements.AnyAsync(a => a.RenewedFromId == agreement.Id
                && (a.Status == ManagementAgreementStatus.PendingBroker || a.Status == ManagementAgreementStatus.Active), ct))
            return Fail(AppErrorCode.Conflict, "A renewal of this agreement already exists.");

        var broker = await _db.Brokers.AsNoTracking().FirstAsync(b => b.Id == agreement.BrokerId, ct);
        var given = request.PropertyIds is { Count: > 0 } ids;
        var units = given ? request.PropertyIds!.Distinct().ToList() : agreement.Properties.Select(p => p.PropertyId).ToList();

        return await SendAsync(broker, agreement.OwnerUserId, units, strictUnits: given,
            request.AutoApproveLimit ?? agreement.AutoApproveLimit, renewedFrom: agreement, ct);
    }

    // ── Units leaving ──────────────────────────────────────────────────────

    public async Task SyncPropertiesAsync(IReadOnlyCollection<Guid>? propertyIds, CancellationToken ct)
    {
        var today = SaudiTime.Today(_time);

        var held = _db.AgreementProperties.Where(x => x.ActiveFrom != null && x.ExitedOn == null);
        if (propertyIds is not null)
            held = held.Where(x => propertyIds.Contains(x.PropertyId));

        var agreementIds = await held.Select(x => x.AgreementId).Distinct().ToListAsync(ct);
        if (agreementIds.Count == 0)
            return;

        // Everything the decisions need, in a few queries: the held units, each with its property
        // and that property's leases. The loop below then runs from memory.
        var agreements = await _db.ManagementAgreements
            .Include(a => a.Broker)
            .Include(a => a.Properties.Where(u => u.ActiveFrom != null && u.ExitedOn == null))
                .ThenInclude(u => u.Property)
                    .ThenInclude(p => p.Contracts)
            .Where(a => agreementIds.Contains(a.Id) && a.Status == ManagementAgreementStatus.Active)
            .AsSplitQuery()
            .ToListAsync(ct);

        // Sent after saving, so nobody is told about a change that didn't stick.
        var notices = new List<(Guid[] To, NewNotification Notification)>();
        var exits = 0;

        foreach (var agreement in agreements)
        {
            var recipients = new[] { agreement.OwnerUserId, agreement.Broker.UserId };

            foreach (var unit in agreement.Properties)
            {
                if (propertyIds is not null && !propertyIds.Contains(unit.PropertyId))
                    continue;

                var property = unit.Property;
                var lease = property.IsDeleted || property.OwnerUserId != agreement.OwnerUserId
                    ? new LeaseState(true, AgreementUnitExitReason.Removed, unit.ContractId, unit.EndsOn)
                    : Evaluate(property.Contracts, unit.ContractId, agreement.BrokerId, today);

                if (lease.Leave)
                {
                    Exit(agreement, unit, lease.Reason!.Value, today);
                    notices.Add((recipients, AgreementNotifications.UnitLeft(agreement.Id, agreement.Number, UnitTitle(property.Title, property.UnitNumber))));
                    exits++;
                }
                else
                {
                    unit.ContractId = lease.ContractId;
                    unit.EndsOn = lease.EndsOn;
                }
            }

            if (agreement.Properties.All(u => u.ExitedOn != null))
            {
                End(agreement, ManagementAgreementStatus.Expired, actorUserId: null, reason: "Every unit has left.");
                AddEvent(agreement.Id, AgreementEventType.Expired, actorUserId: null);
                notices.Add((recipients, AgreementNotifications.Expired(agreement.Id, agreement.Number)));
            }
        }

        await _db.SaveChangesAsync(ct);

        foreach (var (to, notification) in notices)
            await _notifications.NotifyAsync(to, notification, ct);

        if (exits > 0)
            _logger.LogInformation("Agreements: {Count} unit(s) left their agreement.", exits);
    }

    private sealed record LeaseState(bool Leave, AgreementUnitExitReason? Reason, Guid? ContractId, DateOnly? EndsOn);

    // Which lease the unit is tied to now, and whether it has run out. Works on the unit's leases
    // as loaded, so callers fetch them up front rather than per unit.
    private static LeaseState Evaluate(IReadOnlyCollection<Contract> leases, Guid? contractId, Guid brokerId, DateOnly today)
    {
        var tracked = contractId is { } cid ? leases.FirstOrDefault(c => c.Id == cid) : null;

        if (tracked is null)
        {
            // Joining, or vacant when it joined: the lease running now, else the next one to start.
            var active = leases.Where(c => c.Status == ContractStatus.Active).ToList();
            var current = active.Where(c => c.StartDate <= today).MaxBy(c => c.StartDate)
                ?? active.MinBy(c => c.StartDate);
            return current is null
                ? new LeaseState(false, null, null, null)
                : new LeaseState(false, null, current.Id, current.TerminatedOn ?? current.EndDate);
        }

        if (tracked.Status == ContractStatus.Terminated)
            return tracked.TerminatedOn is { } on && on > today
                ? new LeaseState(false, null, tracked.Id, on)
                : new LeaseState(true, AgreementUnitExitReason.LeaseTerminated, tracked.Id, tracked.TerminatedOn);

        // EndDate is the lease's last day; the unit leaves the day after.
        if (tracked.EndDate < today || tracked.Status == ContractStatus.Expired)
        {
            var next = leases
                .Where(c => c.Id != tracked.Id && c.Status == ContractStatus.Active && c.StartDate >= tracked.StartDate)
                .OrderBy(c => c.StartDate)
                .FirstOrDefault();

            if (next is null)
                return new LeaseState(true, AgreementUnitExitReason.LeaseEnded, tracked.Id, tracked.EndDate);

            return next.BrokerId == brokerId
                ? new LeaseState(false, null, next.Id, next.TerminatedOn ?? next.EndDate)
                : new LeaseState(true, AgreementUnitExitReason.LeasedByOtherBroker, tracked.Id, tracked.EndDate);
        }

        return new LeaseState(false, null, tracked.Id, tracked.TerminatedOn ?? tracked.EndDate);
    }

    // Takes the unit out of the agreement (tracked). Saving is the caller's. Never touches
    // Property.BrokerId: a mandate ending doesn't remove the broker from the unit.
    private void Exit(ManagementAgreement agreement, AgreementProperty unit, AgreementUnitExitReason reason, DateOnly today)
    {
        unit.ExitedOn = today;
        unit.ExitReason = reason;

        AddEvent(agreement.Id, AgreementEventType.UnitExited, actorUserId: null, unit.PropertyId, reason.ToString());
    }

    // ── Shared ─────────────────────────────────────────────────────────────

    // Creates the pending mandate and tells the broker. Empty propertyIds → every unit the owner
    // has now. strictUnits: the caller named the units, so an unknown or foreign one is an error
    // (a renewal copying old units just skips ones that have gone).
    private async Task<Result<AgreementDetailsDto>> SendAsync(
        Broker broker, Guid ownerUserId, IReadOnlyCollection<Guid> propertyIds, bool strictUnits,
        decimal? autoApproveLimit, ManagementAgreement? renewedFrom, CancellationToken ct)
    {
        var units = await _db.Properties
            .Where(p => p.OwnerUserId == ownerUserId && !p.IsDeleted && (propertyIds.Count == 0 || propertyIds.Contains(p.Id)))
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (strictUnits && propertyIds.Count > 0 && units.Count != propertyIds.Count)
            return Fail(AppErrorCode.ValidationFailed, "One or more units are unknown or not yours.");

        if (units.Count == 0)
            return Fail(AppErrorCode.ValidationFailed, "You have no units to put in a mandate.");

        var now = _time.GetUtcNow();
        var agreement = new ManagementAgreement
        {
            Id = Guid.NewGuid(),
            Number = await NextNumberAsync(ct),
            BrokerId = broker.Id,
            OwnerUserId = ownerUserId,
            Status = ManagementAgreementStatus.PendingBroker,
            AutoApproveLimit = autoApproveLimit,
            RenewedFromId = renewedFrom?.Id,
            CreatedByUserId = ownerUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Properties = units.Select(id => new AgreementProperty { Id = Guid.NewGuid(), PropertyId = id }).ToList()
        };

        _db.ManagementAgreements.Add(agreement);
        AddEvent(agreement.Id, AgreementEventType.Created, ownerUserId, note: renewedFrom is null ? null : $"Renews {renewedFrom.Number}.");
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Agreement {Number} sent by owner {OwnerId} to broker {BrokerId} over {Count} unit(s).",
            agreement.Number, ownerUserId, broker.Id, units.Count);

        await _notifications.NotifyAsync([broker.UserId],
            AgreementNotifications.NewRequest(agreement.Id, agreement.Number, await NameAsync(ownerUserId, ct), renewedFrom is not null), ct);

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, forOwner: true, ct));
    }

    private sealed record Access(ManagementAgreement Agreement, bool IsOwner);

    // The agreement, tracked with its units, if the caller is its owner (as owner) or its broker
    // (as broker). Anyone else gets null — the same as "doesn't exist".
    private async Task<Access?> AccessAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        var agreement = await _db.ManagementAgreements.Include(a => a.Properties).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agreement is null)
            return null;

        if (role == AccountType.Owner && agreement.OwnerUserId == callerUserId)
            return new Access(agreement, IsOwner: true);

        if (role == AccountType.Broker && await _db.Brokers.AnyAsync(b => b.Id == agreement.BrokerId && b.UserId == callerUserId, ct))
            return new Access(agreement, IsOwner: false);

        return null;
    }

    // The right side asked for the wrong action → 403; a stranger → 404.
    private async Task<Result<AgreementDetailsDto>> NotYoursAsync(Guid id, Guid callerUserId, AccountType role, string message, CancellationToken ct) =>
        await AccessAsync(id, callerUserId, role, ct) is null
            ? Fail(AppErrorCode.NotFound, "Agreement not found.")
            : Fail(AppErrorCode.Forbidden, message);

    private async Task<Broker?> BrokerOfAsync(Guid callerUserId, AccountType role, CancellationToken ct) =>
        role == AccountType.Broker
            ? await _db.Brokers.AsNoTracking().FirstOrDefaultAsync(b => b.UserId == callerUserId, ct)
            : null;

    private async Task<Guid> BrokerUserAsync(Guid brokerId, CancellationToken ct) =>
        await _db.Brokers.Where(b => b.Id == brokerId).Select(b => b.UserId).FirstAsync(ct);

    private async Task<string?> NameAsync(Guid userId, CancellationToken ct) =>
        await _db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

    // MD-2026-000123: per-year sequence. The unique index catches a race; the second caller
    // simply fails and retries the action.
    private async Task<string> NextNumberAsync(CancellationToken ct)
    {
        var prefix = $"MD-{SaudiTime.Today(_time).Year}-";
        var last = await _db.ManagementAgreements
            .Where(a => a.Number.StartsWith(prefix))
            .OrderByDescending(a => a.Number)
            .Select(a => a.Number)
            .FirstOrDefaultAsync(ct);

        var next = last is not null && int.TryParse(last[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{next:D6}";
    }

    private void End(ManagementAgreement agreement, ManagementAgreementStatus status, Guid? actorUserId, string reason)
    {
        agreement.Status = status;
        agreement.EndedOn = SaudiTime.Today(_time);
        agreement.EndedByUserId = actorUserId;
        agreement.EndReason = reason.Trim();
        agreement.UpdatedAt = _time.GetUtcNow();
    }

    private void AddEvent(Guid agreementId, AgreementEventType type, Guid? actorUserId, Guid? propertyId = null, string? note = null) =>
        _db.AgreementEvents.Add(new AgreementEvent
        {
            Id = Guid.NewGuid(),
            AgreementId = agreementId,
            Type = type,
            ActorUserId = actorUserId,
            PropertyId = propertyId,
            Note = note,
            At = _time.GetUtcNow()
        });

    // Active: the day the last held unit leaves, or null while one is vacant (no end yet).
    // Ended: the day it ended. Pending: none.
    public static DateOnly? EndDate(ManagementAgreement a)
    {
        if (a.Status != ManagementAgreementStatus.Active)
            return a.Status == ManagementAgreementStatus.PendingBroker ? null : a.EndedOn;

        var held = a.Properties.Where(p => p.ActiveFrom != null && p.ExitedOn == null).ToList();
        return held.Count == 0 || held.Any(p => p.EndsOn is null) ? null : held.Max(p => p.EndsOn);
    }

    public static AgreementDisplayStatus Display(ManagementAgreement a, DateOnly? end, DateOnly today) => a.Status switch
    {
        ManagementAgreementStatus.PendingBroker => AgreementDisplayStatus.Pending,
        ManagementAgreementStatus.Active when end is { } e && e.DayNumber - today.DayNumber <= EndingSoonDays => AgreementDisplayStatus.EndingSoon,
        ManagementAgreementStatus.Active => AgreementDisplayStatus.Active,
        ManagementAgreementStatus.Expired => AgreementDisplayStatus.Expired,
        ManagementAgreementStatus.Terminated => AgreementDisplayStatus.Terminated,
        ManagementAgreementStatus.Rejected => AgreementDisplayStatus.Rejected,
        _ => AgreementDisplayStatus.Withdrawn
    };

    private static int? DaysLeft(ManagementAgreement a, DateOnly? end, DateOnly today) =>
        a.Status == ManagementAgreementStatus.Active && end is { } e ? Math.Max(0, e.DayNumber - today.DayNumber) : null;

    private static string UnitTitle(string title, string? unitNumber) =>
        string.IsNullOrWhiteSpace(unitNumber) ? title : $"{title} · {unitNumber}";

    private async Task<Dictionary<Guid, AgreementPartyDto>> PartiesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new AgreementPartyDto
            {
                UserId = u.Id,
                Name = u.FullName,
                PhoneNumber = u.CountryCode + u.PhoneNumber,
                ImageUrl = u.ProfileImageUrl
            })
            .ToDictionaryAsync(u => u.UserId, ct);

    private async Task<AgreementDetailsDto> DetailsAsync(Guid id, bool forOwner, CancellationToken ct)
    {
        var a = await _db.ManagementAgreements.AsNoTracking().Include(x => x.Properties).FirstAsync(x => x.Id == id, ct);
        var brokerUserId = await BrokerUserAsync(a.BrokerId, ct);
        var parties = await PartiesAsync([brokerUserId, a.OwnerUserId], ct);
        var today = SaudiTime.Today(_time);
        var end = EndDate(a);

        var propertyIds = a.Properties.Select(p => p.PropertyId).ToList();
        var properties = await _db.Properties
            .AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id, p.Title, p.UnitNumber,
                BuildingName = _db.Buildings.Where(b => b.Id == p.BuildingId).Select(b => b.Name).FirstOrDefault(),
                TenantName = _db.Contracts
                    .Where(c => c.PropertyId == p.Id && c.Status == ContractStatus.Active)
                    .OrderByDescending(c => c.StartDate)
                    .Select(c => _db.Users.Where(u => u.Id == c.TenantUserId).Select(u => u.FullName).FirstOrDefault())
                    .FirstOrDefault()
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var events = await _db.AgreementEvents
            .AsNoTracking()
            .Where(e => e.AgreementId == id)
            .OrderBy(e => e.At)
            .Select(e => new AgreementEventDto
            {
                Type = e.Type,
                At = e.At,
                ActorName = _db.Users.Where(u => u.Id == e.ActorUserId).Select(u => u.FullName).FirstOrDefault(),
                PropertyTitle = _db.Properties.Where(p => p.Id == e.PropertyId).Select(p => p.Title).FirstOrDefault(),
                Note = e.Note
            })
            .ToListAsync(ct);

        var renewedFromNumber = a.RenewedFromId is { } rid
            ? await _db.ManagementAgreements.Where(x => x.Id == rid).Select(x => x.Number).FirstOrDefaultAsync(ct)
            : null;

        var renewalExists = await _db.ManagementAgreements.AnyAsync(x => x.RenewedFromId == a.Id
            && (x.Status == ManagementAgreementStatus.PendingBroker || x.Status == ManagementAgreementStatus.Active), ct);

        var actions = new List<AgreementAction>();
        switch (a.Status)
        {
            case ManagementAgreementStatus.PendingBroker when forOwner:
                actions.Add(AgreementAction.Withdraw);
                break;
            case ManagementAgreementStatus.PendingBroker:
                actions.AddRange([AgreementAction.Accept, AgreementAction.Reject]);
                break;
            case ManagementAgreementStatus.Active:
                actions.Add(AgreementAction.Cancel);
                if (forOwner && !renewalExists) actions.Add(AgreementAction.Renew);
                break;
            case ManagementAgreementStatus.Expired when forOwner && !renewalExists:
                actions.Add(AgreementAction.Renew);
                break;
        }

        return new AgreementDetailsDto
        {
            Id = a.Id,
            Number = a.Number,
            Status = a.Status,
            DisplayStatus = Display(a, end, today),
            BrokerId = a.BrokerId,
            Broker = parties[brokerUserId],
            Owner = parties[a.OwnerUserId],
            AutoApproveLimit = a.AutoApproveLimit,
            StartDate = a.StartDate,
            EndDate = end,
            DaysLeft = DaysLeft(a, end, today),
            EndedOn = a.Status is ManagementAgreementStatus.Active or ManagementAgreementStatus.PendingBroker ? null : a.EndedOn,
            EndReason = a.EndReason,
            RenewedFromId = a.RenewedFromId,
            RenewedFromNumber = renewedFromNumber,
            Units = a.Properties
                .Where(p => properties.ContainsKey(p.PropertyId))
                .Select(p =>
                {
                    var prop = properties[p.PropertyId];
                    var held = p.ActiveFrom != null && p.ExitedOn == null;
                    return new AgreementUnitDto
                    {
                        PropertyId = p.PropertyId,
                        Title = prop.Title,
                        UnitNumber = prop.UnitNumber,
                        BuildingName = prop.BuildingName,
                        TenantName = prop.TenantName,
                        EndsOn = p.EndsOn,
                        DaysLeft = held && p.EndsOn is { } e ? Math.Max(0, e.DayNumber - today.DayNumber) : null,
                        ExitedOn = p.ExitedOn,
                        ExitReason = p.ExitReason
                    };
                })
                .OrderBy(u => u.ExitedOn != null)
                .ThenBy(u => u.EndsOn ?? DateOnly.MaxValue)
                .ToList(),
            Events = events,
            AvailableActions = actions,
            CreatedAt = a.CreatedAt
        };
    }

    private static Result<AgreementDetailsDto> Fail(AppErrorCode code, string message) =>
        Result<AgreementDetailsDto>.Fail(code, message);
}
