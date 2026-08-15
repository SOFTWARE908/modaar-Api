namespace modaar.api.Features.Authentication.Dtos;

public record SendOtpResponseDto
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
}
