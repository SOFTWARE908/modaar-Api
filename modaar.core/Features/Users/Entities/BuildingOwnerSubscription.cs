using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Users.Entities;

// An owner's subscription plan in one building. A building can hold several owners' units, so the
// plan belongs to the pair: each owner's units in that building follow that owner's row. Set by
// the managing broker; the owner can only read it.
//
// No row → DefaultPlan (a unit with no building reads the same). The maintenance auto-approve
// limit is on the owner's mandate (ManagementAgreement), not here.
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

    public Guid? UpdatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
