using System.Text.Json.Serialization;

namespace modaar.api.Features.Payments.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentType
{
    Rent,
    Maintenance
}
