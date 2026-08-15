namespace modaar.api.Features.Authentication.Dtos;

public record ResendOtpResponseDto
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
}
