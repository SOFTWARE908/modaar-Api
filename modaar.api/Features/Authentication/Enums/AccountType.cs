using System.Text.Json.Serialization;

namespace modaar.api.Features.Authentication.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AccountType
{
    Owner,
    Tenant,
    Broker,
    // Reserved for future use; must not be selectable from the mobile app today.
    Technician
}
