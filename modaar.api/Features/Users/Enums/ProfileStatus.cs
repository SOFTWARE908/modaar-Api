using System.Text.Json.Serialization;

namespace modaar.api.Features.Users.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProfileStatus
{
    // Created by confirming a mobile number; the user has not filled in their details yet.
    Incomplete,
    // The user has supplied at least a full name and an account type.
    Complete
}
