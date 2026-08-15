using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Authentication.Dtos;

public record AuthSuccessResponseDto
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required AccountType AccountType { get; init; }
}
