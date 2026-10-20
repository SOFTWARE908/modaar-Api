using System.Text.Json.Serialization;

namespace modaar.api.Features.Users.Enums;

// An owner's subscription: how much of the running of their properties the broker takes on.
// It decides who does what across the app — today maintenance, later other permissions too —
// so it is named for the subscription, not for any one feature.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OwnerSubscriptionPlan
{
    // The owner runs things themselves. Maintenance goes from the tenant straight to the owner;
    // the broker is not in the workflow.
    SelfManaged,

    // The broker runs things and the owner approves and pays. Maintenance: the broker prices
    // the job and the owner approves the quote (unless it is under their auto-approve limit).
    Managed,

    // The full package: the broker runs everything and carries the cost. No owner approvals.
    FullyManaged
}
