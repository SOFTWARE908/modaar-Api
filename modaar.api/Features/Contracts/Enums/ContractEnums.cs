using System.Text.Json.Serialization;

namespace modaar.api.Features.Contracts.Enums;

// The client's ContractStatus has only active and expired. Terminated is kept separate here
// because ending a lease early and letting it run out are different facts, and the difference
// matters for reporting even if both map to "expired" in the app today.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractStatus
{
    Active,
    Expired,
    Terminated
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractRequestType
{
    Renewal,
    Termination
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContractRequestStatus
{
    Pending,
    Accepted,
    Rejected,
    // The tenant withdrew it before anyone answered.
    Cancelled
}
