using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Chat.Dtos;
using modaar.api.Features.Chat.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Chat.Services;

// Who may talk to whom — the one place that decides it. Agreed rule:
//
//   tenant  ↔ owner        of a unit the tenant rents
//   tenant  ↔ broker       managing that unit
//   tenant  ↔ technician   assigned to the tenant's maintenance request (that request only)
//   owner   ↔ broker       managing the owner's property
//
// Two checks, both required: the caller must be related to the property in their active role, and
// the pair (caller role, With) must be one of the lines above. Everything else is refused.
public sealed class ChatAccess
{
    // The allowed pairs, both directions.
    private static readonly HashSet<(AccountType, AccountType)> AllowedPairs =
    [
        (AccountType.Tenant, AccountType.Owner), (AccountType.Owner, AccountType.Tenant),
        (AccountType.Tenant, AccountType.Broker), (AccountType.Broker, AccountType.Tenant),
        (AccountType.Tenant, AccountType.Technician), (AccountType.Technician, AccountType.Tenant),
        (AccountType.Owner, AccountType.Broker), (AccountType.Broker, AccountType.Owner)
    ];

    private readonly ModaarDbContext _db;

    public ChatAccess(ModaarDbContext db) => _db = db;

    // The other person, resolved from the role the caller asked for.
    public sealed record Counterpart(Guid PropertyId, Guid UserId, AccountType Role);

    public async Task<Result<Counterpart>> ResolveAsync(
        Guid callerUserId, AccountType callerRole, StartConversationDto request, CancellationToken ct)
    {
        if (!AllowedPairs.Contains((callerRole, request.With)))
            return Fail(AppErrorCode.Forbidden, $"A {callerRole} cannot start a conversation with a {request.With}.");

        // 1. What the conversation is about decides the property, and on a request or contract,
        //    which tenant and technician are meant.
        Guid propertyId;
        Guid? contextTenantId = null;
        Guid? contextTechnicianUserId = null;

        switch (request.ContextType)
        {
            case ConversationContextType.MaintenanceRequest:
                var maintenance = await _db.MaintenanceRequests.AsNoTracking()
                    .Where(m => m.Id == request.ContextId)
                    .Select(m => new
                    {
                        m.PropertyId,
                        m.TenantUserId,
                        TechnicianUserId = _db.Technicians.Where(t => t.Id == m.AssignedTechnicianId)
                            .Select(t => (Guid?)t.UserId).FirstOrDefault()
                    })
                    .FirstOrDefaultAsync(ct);
                if (maintenance is null)
                    return Fail(AppErrorCode.NotFound, "Maintenance request not found.");

                propertyId = maintenance.PropertyId;
                contextTenantId = maintenance.TenantUserId;
                contextTechnicianUserId = maintenance.TechnicianUserId;
                break;

            case ConversationContextType.Contract:
                var contract = await _db.Contracts.AsNoTracking()
                    .Where(c => c.Id == request.ContextId)
                    .Select(c => new { c.PropertyId, c.TenantUserId })
                    .FirstOrDefaultAsync(ct);
                if (contract is null)
                    return Fail(AppErrorCode.NotFound, "Contract not found.");

                propertyId = contract.PropertyId;
                contextTenantId = contract.TenantUserId;
                break;

            default:
                if (request.PropertyId is { } given)
                {
                    propertyId = given;
                }
                else if (callerRole == AccountType.Tenant)
                {
                    // "Contact the owner" from a tenant with one home needs no property id.
                    var leases = await _db.Contracts.AsNoTracking()
                        .Where(c => c.TenantUserId == callerUserId && c.Status == ContractStatus.Active)
                        .Select(c => c.PropertyId)
                        .Distinct()
                        .ToListAsync(ct);
                    if (leases.Count != 1)
                        return Fail(AppErrorCode.ValidationFailed,
                            leases.Count == 0 ? "You have no active contract." : "You rent more than one unit; send propertyId.");
                    propertyId = leases[0];
                }
                else
                {
                    return Fail(AppErrorCode.ValidationFailed, "propertyId is required for a general conversation.");
                }
                break;
        }

        // A technician only ever talks inside one of their jobs.
        if ((callerRole == AccountType.Technician || request.With == AccountType.Technician)
            && request.ContextType != ConversationContextType.MaintenanceRequest)
            return Fail(AppErrorCode.ValidationFailed, "A conversation with a technician must be about a maintenance request.");

        var property = await _db.Properties.AsNoTracking()
            .Where(p => p.Id == propertyId && !p.IsDeleted)
            .Select(p => new
            {
                p.OwnerUserId,
                BrokerUserId = _db.Brokers.Where(b => b.Id == p.BrokerId).Select(b => (Guid?)b.UserId).FirstOrDefault(),
                ActiveTenantId = _db.Contracts
                    .Where(c => c.PropertyId == p.Id && c.Status == ContractStatus.Active)
                    .Select(c => (Guid?)c.TenantUserId).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
        if (property is null)
            return Fail(AppErrorCode.NotFound, "Property not found.");

        // The tenant this conversation is about: the request's or contract's, else whoever rents
        // the unit now.
        var tenantId = contextTenantId ?? property.ActiveTenantId;

        // 2. The caller must actually be that person on this property.
        var callerIsRelated = callerRole switch
        {
            AccountType.Owner => property.OwnerUserId == callerUserId,
            AccountType.Broker => property.BrokerUserId == callerUserId,
            AccountType.Technician => contextTechnicianUserId == callerUserId,
            // A request's or contract's own tenant, or the unit's current tenant.
            AccountType.Tenant => tenantId == callerUserId,
            _ => false
        };
        if (!callerIsRelated)
            return Fail(AppErrorCode.Forbidden, "You are not part of this property.");

        // 3. Who the other side is.
        Guid? counterpartId = request.With switch
        {
            AccountType.Owner => property.OwnerUserId,
            AccountType.Broker => property.BrokerUserId,
            AccountType.Technician => contextTechnicianUserId,
            AccountType.Tenant => tenantId,
            _ => null
        };

        if (counterpartId is null)
            return Fail(AppErrorCode.NotFound, request.With switch
            {
                AccountType.Broker => "This property has no managing broker.",
                AccountType.Technician => "No technician is assigned to this request yet.",
                _ => $"There is no {request.With} to talk to here."
            });

        if (counterpartId == callerUserId)
            return Fail(AppErrorCode.ValidationFailed, "You cannot start a conversation with yourself.");

        return Result<Counterpart>.Ok(new Counterpart(propertyId, counterpartId.Value, request.With));
    }

    private static Result<Counterpart> Fail(AppErrorCode code, string message) => Result<Counterpart>.Fail(code, message);
}
