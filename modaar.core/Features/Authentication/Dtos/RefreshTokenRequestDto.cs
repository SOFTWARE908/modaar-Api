namespace modaar.api.Features.Authentication.Dtos;

public record RefreshTokenRequestDto
{
    public required string RefreshToken { get; init; }
}
