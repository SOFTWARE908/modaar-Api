namespace modaar.api.Features.Authentication.Entities;

public class OtpCode
{
    public Guid Id { get; set; }

    // Nullable so we can issue an OTP for an unknown identifier without leaking user existence to the caller.
    public Guid? UserId { get; set; }

    // Phone, email, or nationalId — whichever the login flow used.
    public string Identifier { get; set; } = null!;

    // Set for phone OTPs only. Kept alongside the identifier so an unknown number can be turned
    // into a real account at verification time, when the login request is no longer available.
    public string? CountryCode { get; set; }

    public string CodeHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public int Attempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
