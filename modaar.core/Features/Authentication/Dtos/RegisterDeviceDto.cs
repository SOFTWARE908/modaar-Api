namespace modaar.api.Features.Authentication.Dtos;

// Sent after every login, and again whenever Firebase hands the app a new token.
public record RegisterDeviceDto
{
    // The app's own install id — the same one used for biometric login.
    public required string DeviceId { get; init; }

    // From FirebaseMessaging.instance.getToken().
    public required string FcmToken { get; init; }

    // "ios" / "android".
    public string? Platform { get; init; }
}
