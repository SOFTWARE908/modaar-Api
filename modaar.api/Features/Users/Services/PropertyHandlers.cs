using Microsoft.EntityFrameworkCore;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

// Who runs a property day to day, by the owner's subscription: the owner on SelfManaged (or when
// the property has no broker), the managing broker otherwise. The same rule maintenance follows,
// for notices that go "to the owner or the broker".
public static class PropertyHandlers
{
    public static async Task<Dictionary<Guid, Guid>> ResolveAsync(
        ModaarDbContext db, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct)
    {
        var rows = await db.Properties
            .AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.OwnerUserId,
                Plan = db.Users.Where(u => u.Id == p.OwnerUserId).Select(u => u.SubscriptionPlan).FirstOrDefault(),
                BrokerUserId = db.Brokers.Where(b => b.Id == p.BrokerId).Select(b => (Guid?)b.UserId).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            r => r.Id,
            r => r.BrokerUserId is { } broker && r.Plan != OwnerSubscriptionPlan.SelfManaged ? broker : r.OwnerUserId);
    }
}
