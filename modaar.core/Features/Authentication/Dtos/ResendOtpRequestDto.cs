using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Authentication.Dtos;

public record ResendOtpRequestDto
{
    // Phone, email, or nationalId depending on the login method used.
    public required string Identifier { get; init; }
    // Only PhoneOtp or NationalIdOtp are valid here; EmailPassword has no OTP to resend.
    public required LoginMethod LoginMethod { get; init; }
}
