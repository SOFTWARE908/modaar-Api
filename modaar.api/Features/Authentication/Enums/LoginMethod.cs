using System.Text.Json.Serialization;

namespace modaar.api.Features.Authentication.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LoginMethod
{
    EmailPassword,
    PhoneOtp,
    NationalIdOtp
}
