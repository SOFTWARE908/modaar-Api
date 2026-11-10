namespace modaar.api.Common.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenExpiresMinutes { get; set; } = 60;
    public int RefreshTokenExpiresDays { get; set; } = 30;
}
