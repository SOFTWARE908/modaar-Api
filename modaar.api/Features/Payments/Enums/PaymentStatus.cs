using System.Text.Json.Serialization;

namespace modaar.api.Features.Payments.Enums;

// The client only shows "paid" today. The other two exist so an upload can carry a payment that
// failed or is still settling rather than being forced to drop it.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentStatus
{
    Paid,
    Pending,
    Failed
}
