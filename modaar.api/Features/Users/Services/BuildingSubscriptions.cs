using Microsoft.EntityFrameworkCore;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

// Reads the plan a unit runs under: its owner's row for its building (BuildingOwnerSubscription),
// or the default when there is none.
public static class BuildingSubscriptions
{
    public sealed record Terms(OwnerSubscriptionPlan Plan);

    public static readonly Terms Default = new(BuildingOwnerSubscription.DefaultPlan);

    public static async Task<Terms> ForPropertyAsync(ModaarDbContext db, Guid propertyId, CancellationToken ct)
    {
        var plan = await db.Properties
            .AsNoTracking()
            .Where(p => p.Id == propertyId)
            .Select(p => db.BuildingOwnerSubscriptions
                .Where(s => s.BuildingId == p.BuildingId && s.OwnerUserId == p.OwnerUserId)
                .Select(s => (OwnerSubscriptionPlan?)s.Plan)
                .FirstOrDefault())
            .FirstOrDefaultAsync(ct);

        return plan is { } p ? new Terms(p) : Default;
    }
}
