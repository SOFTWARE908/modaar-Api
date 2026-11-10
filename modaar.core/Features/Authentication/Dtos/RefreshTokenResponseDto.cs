using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Authentication.Dtos;

public record RefreshTokenResponseDto
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }

    // The role this new token carries. Null means "pick one" (select-role): the role the session
    // had was removed from the account and more than one is left.
    public AccountType? ActiveRole { get; init; }
    public required IReadOnlyList<AccountType> AccountTypes { get; init; }
}
