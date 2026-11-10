namespace modaar.api.Features.App
{
    public sealed class AppVersionOptions
    {
        public const string SectionName = "AppVersion";

        public string? MinimumRequiredVersion { get; set; }
        public string? MinimumVersionRequiredMessage { get; set; }
        public string? MinimumVersionOptional { get; set; }
        public string? MinimumVersionOptionalMessage { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }
}