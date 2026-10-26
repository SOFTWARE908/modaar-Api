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
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Agreements.Services;

public interface IManagementAgreementService
{
    // Broker: owners' requests to them.
    Task<Result<PagedResultDto<ManagementRequestItemDto>>> ListRequestsAsync(
        Guid callerUserId, AccountType role, ManagementRequestsRequestDto query, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> AcceptRequestAsync(
        Guid requestId, Guid callerUserId, AccountType role, AcceptManagementRequestDto terms, CancellationToken ct);
    Task<Result<bool>> RejectRequestAsync(
        Guid requestId, Guid callerUserId, AccountType role, string reason, CancellationToken ct);

    // Broker proposes without a request.
    Task<Result<AgreementDetailsDto>> CreateAsync(
        Guid callerUserId, AccountType role, CreateAgreementDto request, CancellationToken ct);

    // Both sides.
    Task<Result<PagedResultDto<AgreementListItemDto>>> ListAsync(
        Guid callerUserId, AccountType role, AgreementsRequestDto query, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> GetAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct);

    Task<Result<AgreementDetailsDto>> ApproveAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> RejectAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> CancelAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct);
    Task<Result<AgreementDetailsDto>> RenewAsync(Guid id, Guid callerUserId, AccountType role, RenewAgreementDto request, CancellationToken ct);

    // Re-reads the leases of these units and lets any whose lease is over leave. Called by the
    // daily job and right after a lease is ended early.
    Task SyncPropertiesAsync(IReadOnlyCollection<Guid>? propertyIds, CancellationToken ct);

    // A broker added a unit for a client: it joins their active agreement.
    Task AttachNewUnitAsync(Guid brokerId, Guid ownerUserId, Guid propertyId, Guid actorUserId, CancellationToken ct);
}

// The rules, in one place:
// - The owner asks (BrokerManagementRequests), the broker answers with terms → pendingOwner;
//   or the broker proposes directly. Only the owner's approval makes it active.
// - On approval each unit is "held": Property.BrokerId points at the broker, and the unit leaves
//   when its lease does — the current lease, or the first one signed on it if it was vacant.
//   A lease ended early → it leaves that day. A next lease made by the same broker keeps it.
// - When the last unit has left the agreement is expired. Either side may cancel an active one
//   with a reason; every unit leaves that day.
// - Leaving clears Property.BrokerId, so open maintenance, transfer reviews and reminders fall
//   to the owner by the existing rules (PropertyHandlers, MaintenanceFacts).
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

    // ── Requests ───────────────────────────────────────────────────────────

    public async Task<Result<PagedResultDto<ManagementRequestItemDto>>> ListRequestsAsync(
        Guid callerUserId, AccountType role, ManagementRequestsRequestDto query, CancellationToken ct)
    {
        if (await BrokerOfAsync(callerUserId, role, ct) is not { } broker)
            return Result<PagedResultDto<ManagementRequestItemDto>>.Fail(AppErrorCode.Forbidden, "Only a broker receives management requests.");

        var status = query.Status ?? BrokerManagementRequestStatus.Pending;
        var rows = _db.BrokerManagementRequests.AsNoTracking().Where(r => r.BrokerId == broker.Id && r.Status == status);

        var total = await rows.CountAsync(ct);
        var page = await rows
            .OrderByDescending(r => r.CreatedAt)
            .Skip(query.SkipCount).Take(query.MaxResultCount)
            .Select(r => new
            {
                r.Id, r.Status, r.OwnerUserId, r.PropertyIds, r.DecisionReason, r.CreatedAt,
                Owner = _db.Users.Where(u => u.Id == r.OwnerUserId)
                    .Select(u => new { u.FullName, Phone = u.CountryCode + u.PhoneNumber, u.ProfileImageUrl }).FirstOrDefault(),
                OwnedUnits = _db.Properties.Count(p => p.OwnerUserId == r.OwnerUserId && !p.IsDeleted),
                AgreementId = _db.ManagementAgreements.Where(a => a.RequestId == r.Id).Select(a => (Guid?)a.Id).FirstOrDefault()
            })
            .ToListAsync(ct);

        return Result<PagedResultDto<ManagementRequestItemDto>>.Ok(new PagedResultDto<ManagementRequestItemDto>
        {
            TotalCount = total,
            Items = page.Select(r => new ManagementRequestItemDto
            {
                Id = r.Id,
                Status = r.Status,
                Owner = new AgreementPartyDto { UserId = r.OwnerUserId, Name = r.Owner?.FullName, PhoneNumber = r.Owner?.Phone, ImageUrl = r.Owner?.ProfileImageUrl },
                WholePortfolio = r.PropertyIds.Count == 0,
                UnitsCount = r.PropertyIds.Count == 0 ? r.OwnedUnits : r.PropertyIds.Count,
                DecisionReason = r.DecisionReason,
                AgreementId = r.AgreementId,
                CreatedAt = r.CreatedAt
            }).ToList()
        });
    }

    public async Task<Result<AgreementDetailsDto>> AcceptRequestAsync(
        Guid requestId, Guid callerUserId, AccountType role, AcceptManagementRequestDto terms, CancellationToken ct)
    {
        if (await BrokerOfAsync(callerUserId, role, ct) is not { } broker)
            return Fail(AppErrorCode.Forbidden, "Only a broker answers management requests.");

        var request = await _db.BrokerManagementRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.BrokerId == broker.Id, ct);
        if (request is null)
            return Fail(AppErrorCode.NotFound, "Request not found.");
        if (request.Status != BrokerManagementRequestStatus.Pending)
            return Fail(AppErrorCode.Conflict, "This request has already been answered.");

        // The broker may narrow what was asked, not widen it.
        var asked = request.PropertyIds;
        var chosen = terms.PropertyIds is { Count: > 0 } ids ? ids.Distinct().ToList() : asked;
        if (asked.Count > 0 && chosen.Except(asked).Any())
            return Fail(AppErrorCode.ValidationFailed, "propertyIds must be among the units the owner asked about.");

        var now = _time.GetUtcNow();
        var created = await ProposeAsync(broker, request.OwnerUserId, chosen, terms, callerUserId, request.Id, renewedFrom: null, ct);
        if (!created.Success)
            return created;

        request.Status = BrokerManagementRequestStatus.Accepted;
        request.DecidedByUserId = callerUserId;
        request.DecidedAt = now;
        request.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        return created;
    }

    public async Task<Result<bool>> RejectRequestAsync(
        Guid requestId, Guid callerUserId, AccountType role, string reason, CancellationToken ct)
    {
        if (await BrokerOfAsync(callerUserId, role, ct) is not { } broker)
            return Result<bool>.Fail(AppErrorCode.Forbidden, "Only a broker answers management requests.");

        var request = await _db.BrokerManagementRequests.FirstOrDefaultAsync(r => r.Id == requestId && r.BrokerId == broker.Id, ct);
        if (request is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Request not found.");
        if (request.Status != BrokerManagementRequestStatus.Pending)
            return Result<bool>.Fail(AppErrorCode.Conflict, "This request has already been answered.");

        var now = _time.GetUtcNow();
        request.Status = BrokerManagementRequestStatus.Rejected;
        request.DecidedByUserId = callerUserId;
        request.DecidedAt = now;
        request.DecisionReason = reason.Trim();
        request.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync([request.OwnerUserId], AgreementNotifications.RequestRejected(await NameAsync(callerUserId, ct)), ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<AgreementDetailsDto>> CreateAsync(
        Guid callerUserId, AccountType role, CreateAgreementDto request, CancellationToken ct)
    {
        if (await BrokerOfAsync(callerUserId, role, ct) is not { } broker)
            return Fail(AppErrorCode.Forbidden, "Only a broker proposes an agreement.");

        var isOwner = await _db.Users.AnyAsync(u =>
            u.Id == request.OwnerUserId && !u.IsDeleted && u.AccountTypes.Contains(AccountType.Owner), ct);
        if (!isOwner)
            return Fail(AppErrorCode.NotFound, "Owner not found.");

        return await ProposeAsync(broker, request.OwnerUserId, request.PropertyIds?.Distinct().ToList() ?? [],
            request, callerUserId, requestId: null, renewedFrom: null, ct);
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
            .OrderBy(a => a.Status == ManagementAgreementStatus.PendingOwner ? 0 : a.Status == ManagementAgreementStatus.Active ? 1 : 2)
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
                    UnitsCount = a.Status == ManagementAgreementStatus.PendingOwner
                        ? a.Properties.Count
                        : a.Properties.Count(p => p.ActiveFrom != null && p.ExitedOn == null),
                    ManagementFeePercent = a.ManagementFeePercent,
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

    // ── Owner's answer ─────────────────────────────────────────────────────

    public async Task<Result<AgreementDetailsDto>> ApproveAsync(Guid id, Guid callerUserId, AccountType role, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { IsOwner: true } access)
            return await NotYoursAsync(id, callerUserId, role, "Only the owner approves an agreement.", ct);

        var agreement = access.Agreement;
        if (agreement.Status != ManagementAgreementStatus.PendingOwner)
            return Fail(AppErrorCode.Conflict, "This agreement isn't waiting for approval.");

        var today = SaudiTime.Today(_time);
        var now = _time.GetUtcNow();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // A renewal takes over the units the old agreement still holds: release them first so the
        // one-holder index never sees two.
        ManagementAgreement? old = null;
        if (agreement.RenewedFromId is { } oldId)
        {
            old = await _db.ManagementAgreements.Include(a => a.Properties).FirstAsync(a => a.Id == oldId, ct);
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

        // Units no longer the owner's drop out of the proposal.
        foreach (var unit in agreement.Properties.ToList())
        {
            if (!properties.TryGetValue(unit.PropertyId, out var p) || p.IsDeleted || p.OwnerUserId != agreement.OwnerUserId)
                agreement.Properties.Remove(unit);
        }

        var heldElsewhere = await _db.AgreementProperties
            .Where(x => propertyIds.Contains(x.PropertyId) && x.ActiveFrom != null && x.ExitedOn == null && x.AgreementId != agreement.Id)
            .Select(x => x.PropertyId)
            .ToListAsync(ct);

        // A unit run by another broker outside any agreement can't be taken over silently either.
        var runByOther = properties.Values
            .Where(p => p.BrokerId is { } b && b != agreement.BrokerId && agreement.Properties.Any(u => u.PropertyId == p.Id))
            .Select(p => p.Id);

        var blocked = heldElsewhere.Concat(runByOther).Distinct().ToList();
        if (blocked.Count > 0)
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return Fail(AppErrorCode.Conflict,
                $"{blocked.Count} unit(s) in this agreement are managed under another agreement: {string.Join(", ", blocked)}.");
        }

        if (agreement.Properties.Count == 0)
        {
            await tx.RollbackAsync(ct);
            _db.ChangeTracker.Clear();
            return Fail(AppErrorCode.Conflict, "None of the units in this agreement can be managed any more.");
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
        AddEvent(agreement.Id, AgreementEventType.Approved, callerUserId);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Agreement {Number} approved: broker {BrokerId} now runs {Count} unit(s) of owner {OwnerId}.",
            agreement.Number, agreement.BrokerId, agreement.Properties.Count, agreement.OwnerUserId);

        await _notifications.NotifyAsync([await BrokerUserAsync(agreement.BrokerId, ct)],
            AgreementNotifications.Approved(agreement.Id, agreement.Number, await NameAsync(callerUserId, ct)), ct);

        // A unit whose lease is already over leaves straight away.
        await SyncPropertiesAsync(agreement.Properties.Select(p => p.PropertyId).ToList(), ct);

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, forOwner: true, ct));
    }

    public async Task<Result<AgreementDetailsDto>> RejectAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { IsOwner: true } access)
            return await NotYoursAsync(id, callerUserId, role, "Only the owner rejects an agreement.", ct);

        var agreement = access.Agreement;
        if (agreement.Status != ManagementAgreementStatus.PendingOwner)
            return Fail(AppErrorCode.Conflict, "This agreement isn't waiting for approval.");

        End(agreement, ManagementAgreementStatus.Rejected, callerUserId, reason);
        AddEvent(agreement.Id, AgreementEventType.Rejected, callerUserId, note: reason.Trim());
        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync([await BrokerUserAsync(agreement.BrokerId, ct)],
            AgreementNotifications.Rejected(agreement.Id, agreement.Number, await NameAsync(callerUserId, ct)), ct);

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, forOwner: true, ct));
    }

    // ── Either side ────────────────────────────────────────────────────────

    // Broker on a pending one → withdrawn. Either side on an active one → terminated, every unit
    // leaves today.
    public async Task<Result<AgreementDetailsDto>> CancelAsync(Guid id, Guid callerUserId, AccountType role, string reason, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { } access)
            return Fail(AppErrorCode.NotFound, "Agreement not found.");

        var agreement = access.Agreement;
        var brokerUserId = await BrokerUserAsync(agreement.BrokerId, ct);
        var callerName = await NameAsync(callerUserId, ct);

        if (agreement.Status == ManagementAgreementStatus.PendingOwner)
        {
            if (access.IsOwner)
                return Fail(AppErrorCode.Conflict, "To turn down a pending agreement, reject it.");

            End(agreement, ManagementAgreementStatus.Withdrawn, callerUserId, reason);
            AddEvent(agreement.Id, AgreementEventType.Withdrawn, callerUserId, note: reason.Trim());
            await _db.SaveChangesAsync(ct);
            await _notifications.NotifyAsync([agreement.OwnerUserId],
                AgreementNotifications.Withdrawn(agreement.Id, agreement.Number, callerName), ct);
            return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, access.IsOwner, ct));
        }

        if (agreement.Status != ManagementAgreementStatus.Active)
            return Fail(AppErrorCode.Conflict, "This agreement has already ended.");

        var today = SaudiTime.Today(_time);
        var held = agreement.Properties.Where(p => p.ActiveFrom != null && p.ExitedOn == null).ToList();
        var heldIds = held.Select(u => u.PropertyId).ToList();
        var properties = await _db.Properties.Where(p => heldIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var unit in held)
            Exit(agreement, unit, properties[unit.PropertyId], AgreementUnitExitReason.AgreementTerminated, today);

        End(agreement, ManagementAgreementStatus.Terminated, callerUserId, reason);
        AddEvent(agreement.Id, AgreementEventType.Terminated, callerUserId, note: reason.Trim());

        // A renewal waiting on this one has nothing left to renew.
        var pendingRenewals = await _db.ManagementAgreements
            .Where(a => a.RenewedFromId == agreement.Id && a.Status == ManagementAgreementStatus.PendingOwner)
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

    // Broker asks to carry on: a new pending agreement over the same units, terms copied unless
    // given. Works on an active agreement and on one that expired (its units may come back).
    public async Task<Result<AgreementDetailsDto>> RenewAsync(Guid id, Guid callerUserId, AccountType role, RenewAgreementDto request, CancellationToken ct)
    {
        if (await AccessAsync(id, callerUserId, role, ct) is not { IsOwner: false } access)
            return await NotYoursAsync(id, callerUserId, role, "Only the broker asks for a renewal.", ct);

        var agreement = access.Agreement;
        if (agreement.Status is not (ManagementAgreementStatus.Active or ManagementAgreementStatus.Expired))
            return Fail(AppErrorCode.Conflict, "Only an active or expired agreement can be renewed.");

        if (await _db.ManagementAgreements.AnyAsync(a => a.RenewedFromId == agreement.Id
                && (a.Status == ManagementAgreementStatus.PendingOwner || a.Status == ManagementAgreementStatus.Active), ct))
            return Fail(AppErrorCode.Conflict, "A renewal of this agreement already exists.");

        var terms = new AgreementTermsDto
        {
            ManagementFeePercent = request.ManagementFeePercent ?? agreement.ManagementFeePercent ?? 0m,
            LeasingFeeType = request.LeasingFeeValue is null ? agreement.LeasingFeeType : request.LeasingFeeType,
            LeasingFeeValue = request.LeasingFeeValue ?? agreement.LeasingFeeValue,
            DocumentUrl = request.DocumentUrl ?? agreement.DocumentUrl
        };

        var broker = await _db.Brokers.AsNoTracking().FirstAsync(b => b.Id == agreement.BrokerId, ct);
        var units = agreement.Properties.Select(p => p.PropertyId).ToList();

        return await ProposeAsync(broker, agreement.OwnerUserId, units, terms, callerUserId, requestId: null, renewedFrom: agreement, ct);
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
                    Exit(agreement, unit, property, lease.Reason!.Value, today);
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

    public async Task AttachNewUnitAsync(Guid brokerId, Guid ownerUserId, Guid propertyId, Guid actorUserId, CancellationToken ct)
    {
        var agreement = await _db.ManagementAgreements
            .Where(a => a.BrokerId == brokerId && a.OwnerUserId == ownerUserId && a.Status == ManagementAgreementStatus.Active)
            .OrderByDescending(a => a.ApprovedAt)
            .FirstOrDefaultAsync(ct);

        if (agreement is null)
            return;

        _db.AgreementProperties.Add(new AgreementProperty
        {
            Id = Guid.NewGuid(),
            AgreementId = agreement.Id,
            PropertyId = propertyId,
            ActiveFrom = SaudiTime.Today(_time)
        });
        AddEvent(agreement.Id, AgreementEventType.UnitAdded, actorUserId, propertyId);
        await _db.SaveChangesAsync(ct);
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

    // Takes the unit out and the broker off the property (both tracked). Saving is the caller's.
    private void Exit(ManagementAgreement agreement, AgreementProperty unit, Property property, AgreementUnitExitReason reason, DateOnly today)
    {
        unit.ExitedOn = today;
        unit.ExitReason = reason;

        if (property.BrokerId == agreement.BrokerId)
        {
            property.BrokerId = null;
            property.UpdatedAt = _time.GetUtcNow();
        }

        AddEvent(agreement.Id, AgreementEventType.UnitExited, actorUserId: null, unit.PropertyId, reason.ToString());
    }

    // ── Shared ─────────────────────────────────────────────────────────────

    private async Task<Result<AgreementDetailsDto>> ProposeAsync(
        Broker broker, Guid ownerUserId, IReadOnlyCollection<Guid> propertyIds, AgreementTermsDto terms,
        Guid actorUserId, Guid? requestId, ManagementAgreement? renewedFrom, CancellationToken ct)
    {
        // Empty → every unit the owner has now.
        var units = await _db.Properties
            .Where(p => p.OwnerUserId == ownerUserId && !p.IsDeleted && (propertyIds.Count == 0 || propertyIds.Contains(p.Id)))
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (propertyIds.Count > 0 && units.Count != propertyIds.Count && renewedFrom is null)
            return Fail(AppErrorCode.ValidationFailed, "One or more units are unknown or not this owner's.");

        if (units.Count == 0)
            return Fail(AppErrorCode.ValidationFailed, "The owner has no units to manage.");

        var now = _time.GetUtcNow();
        var agreement = new ManagementAgreement
        {
            Id = Guid.NewGuid(),
            Number = await NextNumberAsync(ct),
            BrokerId = broker.Id,
            OwnerUserId = ownerUserId,
            Status = ManagementAgreementStatus.PendingOwner,
            ManagementFeePercent = terms.ManagementFeePercent,
            LeasingFeeType = terms.LeasingFeeType,
            LeasingFeeValue = terms.LeasingFeeValue,
            DocumentUrl = terms.DocumentUrl?.Trim(),
            RequestId = requestId,
            RenewedFromId = renewedFrom?.Id,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now,
            Properties = units.Select(id => new AgreementProperty { Id = Guid.NewGuid(), PropertyId = id }).ToList()
        };

        _db.ManagementAgreements.Add(agreement);
        AddEvent(agreement.Id, AgreementEventType.Created, actorUserId, note: renewedFrom is null ? null : $"Renews {renewedFrom.Number}.");
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Agreement {Number} proposed by broker {BrokerId} to owner {OwnerId} over {Count} unit(s).",
            agreement.Number, broker.Id, ownerUserId, units.Count);

        await _notifications.NotifyAsync([ownerUserId],
            AgreementNotifications.AwaitingOwner(agreement.Id, agreement.Number, await NameAsync(broker.UserId, ct), renewedFrom is not null), ct);

        return Result<AgreementDetailsDto>.Ok(await DetailsAsync(agreement.Id, forOwner: false, ct));
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
            return a.Status == ManagementAgreementStatus.PendingOwner ? null : a.EndedOn;

        var held = a.Properties.Where(p => p.ActiveFrom != null && p.ExitedOn == null).ToList();
        return held.Count == 0 || held.Any(p => p.EndsOn is null) ? null : held.Max(p => p.EndsOn);
    }

    public static AgreementDisplayStatus Display(ManagementAgreement a, DateOnly? end, DateOnly today) => a.Status switch
    {
        ManagementAgreementStatus.PendingOwner => AgreementDisplayStatus.Pending,
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
            && (x.Status == ManagementAgreementStatus.PendingOwner || x.Status == ManagementAgreementStatus.Active), ct);

        var actions = new List<AgreementAction>();
        switch (a.Status)
        {
            case ManagementAgreementStatus.PendingOwner when forOwner:
                actions.AddRange([AgreementAction.Approve, AgreementAction.Reject]);
                break;
            case ManagementAgreementStatus.PendingOwner:
                actions.Add(AgreementAction.Withdraw);
                break;
            case ManagementAgreementStatus.Active:
                actions.Add(AgreementAction.Cancel);
                if (!forOwner && !renewalExists) actions.Add(AgreementAction.Renew);
                break;
            case ManagementAgreementStatus.Expired when !forOwner && !renewalExists:
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
            ManagementFeePercent = a.ManagementFeePercent,
            LeasingFeeType = a.LeasingFeeType,
            LeasingFeeValue = a.LeasingFeeValue,
            DocumentUrl = a.DocumentUrl,
            StartDate = a.StartDate,
            EndDate = end,
            DaysLeft = DaysLeft(a, end, today),
            EndedOn = a.Status is ManagementAgreementStatus.Active or ManagementAgreementStatus.PendingOwner ? null : a.EndedOn,
            EndReason = a.EndReason,
            RenewedFromId = a.RenewedFromId,
            RenewedFromNumber = renewedFromNumber,
            RequestId = a.RequestId,
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
