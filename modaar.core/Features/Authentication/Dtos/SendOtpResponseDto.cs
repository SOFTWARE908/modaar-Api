namespace modaar.api.Features.Authentication.Dtos;

public record SendOtpResponseDto
{
    public required bool Success { get; init; }
    public required string Message { get; init; }

    // Exactly what to send back to /verify-otp and /resend-otp: the full international number for
    // a phone login, the national ID otherwise. Saves the client reassembling it.
    public required string Identifier { get; init; }

    public required int OtpExpiresInSeconds { get; init; }
}
