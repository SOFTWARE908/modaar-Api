namespace modaar.api.Features.Authentication.Dtos;

public record VerifyOtpRequestDto
{
    public required string OtpCode { get; init; }
    // Phone, email, or nationalId depending on the login method used.
    public required string Identifier { get; init; }
}
