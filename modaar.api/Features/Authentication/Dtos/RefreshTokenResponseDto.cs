namespace modaar.api.Features.Authentication.Dtos;

public record RefreshTokenResponseDto
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
}
