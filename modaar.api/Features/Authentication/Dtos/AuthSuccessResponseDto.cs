using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Authentication.Dtos;

public record AuthSuccessResponseDto
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required AccountType AccountType { get; init; }

    // True when this verification created the account, so the app can route straight to the
    // profile screen instead of the home screen.
    public required bool IsNewUser { get; init; }

    public required ProfileStatus ProfileStatus { get; init; }
}
