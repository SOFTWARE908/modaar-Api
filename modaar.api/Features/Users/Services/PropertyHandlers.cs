using Microsoft.EntityFrameworkCore;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

// Who handles a property's rent and leases: always its owner. A broker's mandate covers
// maintenance only, so overdue rent, renewal reminders and bank-transfer reviews go to the owner
// even when a broker runs the unit. Maintenance has its own rule (MaintenanceStages).
public static class PropertyHandlers
{
    public static async Task<Dictionary<Guid, Guid>> ResolveAsync(
        ModaarDbContext db, IReadOnlyCollection<Guid> propertyIds, CancellationToken ct) =>
        await db.Properties
            .AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.OwnerUserId, ct);
}
