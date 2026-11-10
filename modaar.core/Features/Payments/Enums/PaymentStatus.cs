using System.Text.Json.Serialization;

namespace modaar.api.Features.Payments.Enums;

// Pending = owed. UnderReview = the tenant sent a bank-transfer receipt and the owner or broker
// hasn't confirmed it yet. Failed exists so an upload can carry a payment that failed rather than
// being forced to drop it.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Paid,
    Pending,
    Failed,
    UnderReview
}
