using System.Text.Json.Serialization;

namespace modaar.api.Features.Payments.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentDisplayStatus
{
    Paid,
    Due,
    Overdue,
    // A bank-transfer receipt is waiting for the owner or broker to confirm it.
    UnderReview
}
