using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Users.Entities;

// An owner's subscription plan in one building. A building can hold several owners' units, so the
// plan belongs to the pair: each owner's units in that building follow that owner's row. Set by
// the managing broker; the owner can only read it.
//
// No row → DefaultPlan with no auto-approve limit (a unit with no building reads the same).
public class BuildingOwnerSubscription
{
    public const OwnerSubscriptionPlan DefaultPlan = OwnerSubscriptionPlan.Managed;

    public Guid Id { get; set; }
    public Guid BuildingId { get; set; }
    public Guid OwnerUserId { get; set; }

    // Navigation, never lazy-loaded: Include it or project through it.
    public Building Building { get; set; } = null!;
    public User Owner { get; set; } = null!;

    public OwnerSubscriptionPlan Plan { get; set; } = DefaultPlan;

    // Managed plan: quotes at or under this total are approved without asking the owner.
    // Null = every quote goes to the owner.
    public decimal? AutoApproveLimit { get; set; }

    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
