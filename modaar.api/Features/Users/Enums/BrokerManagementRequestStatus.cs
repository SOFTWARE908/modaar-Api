using System.Text.Json.Serialization;

namespace modaar.api.Features.Users.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BrokerManagementRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    // The owner withdrew it before anyone answered.
    Cancelled
}
