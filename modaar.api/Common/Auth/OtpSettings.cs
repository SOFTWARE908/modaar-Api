namespace modaar.api.Common.Auth;

public sealed class OtpSettings
{
    public const string SectionName = "OtpSettings";

    public int LengthDigits { get; set; } = 6;
    public int ExpiresInMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 30;

    // Dev-only switch: when true, OTP generation returns FixedCode instead of a random code,
    // and "delivery" is a no-op (the code is logged). MUST be false in production.
    public bool UseFixedCode { get; set; }
    public string FixedCode { get; set; } = "123456";
}
