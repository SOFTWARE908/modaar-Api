using System.Text.Json.Serialization;

namespace modaar.api.Features.Contracts.Enums;

// Whether the renew button is live. Computed, never stored: the lease must be Active, inside
// the renewal window before EndDate, and have no renewal request already pending.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractRenewalAvailability
{
    Available,
    Unavailable,
    // A request is already in, so the button shows as waiting rather than offering again.
    Pending
}
