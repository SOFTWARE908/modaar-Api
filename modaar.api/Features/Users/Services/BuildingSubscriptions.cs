using Microsoft.EntityFrameworkCore;
using modaar.api.Features.Users.Entities;
using modaar.api.Features.Users.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Users.Services;

// Reads the plan a unit runs under: its owner's row for its building (BuildingOwnerSubscription),
// or the default when there is none.
public static class BuildingSubscriptions
{
    public sealed record Terms(OwnerSubscriptionPlan Plan, decimal? AutoApproveLimit);

    public static readonly Terms Default = new(BuildingOwnerSubscription.DefaultPlan, null);

    public static async Task<Terms> ForPropertyAsync(ModaarDbContext db, Guid propertyId, CancellationToken ct)
    {
        var row = await db.Properties
            .AsNoTracking()
            .Where(p => p.Id == propertyId)
            .Select(p => db.BuildingOwnerSubscriptions
                .Where(s => s.BuildingId == p.BuildingId && s.OwnerUserId == p.OwnerUserId)
                .Select(s => new { s.Plan, s.AutoApproveLimit })
                .FirstOrDefault())
            .FirstOrDefaultAsync(ct);

        return row is null ? Default : new Terms(row.Plan, row.AutoApproveLimit);
    }
}
