namespace modaar.api.Features.Authentication.Dtos;

public record LogoutRequestDto
{
    // This phone's FCM token. Sent on logout so pushes stop arriving on it.
    public string? DeviceToken { get; init; }
}
