namespace modaar.api.Features.Authentication.Dtos;

public record LogoutRequestDto
{
    // FCM token to unregister, carried over from the legacy API. Nothing consumes it yet — push
    // notifications are not implemented — but the client already sends it.
    public string? DeviceToken { get; init; }
}
