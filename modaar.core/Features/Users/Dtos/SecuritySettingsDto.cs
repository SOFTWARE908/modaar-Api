namespace modaar.api.Features.Users.Dtos
{
    public record SecuritySettingsDto
    {
        public required bool RememberMe { get; init; }
        public required bool FaceIdEnabled { get; init; }
        public required bool BiometricEnabled { get; init; }
    }

    // Partial update: an omitted flag is left as it was.
    public record UpdateSecuritySettingsDto
    {
        public bool? RememberMe { get; init; }
        public bool? FaceIdEnabled { get; init; }
        public bool? BiometricEnabled { get; init; }
    }
}