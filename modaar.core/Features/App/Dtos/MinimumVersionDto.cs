
namespace modaar.api.Features.App.Dtos
{
    // Every field optional, per the spec. Two tiers: Required blocks the app until
    // the user updates; Optional prompts but lets them continue.
    public record MinimumVersionDto
    {
        public string? MinimumRequiredVersion { get; init; }
        public string? MinimumVersionRequiredMessage { get; init; }

        public string? MinimumVersionOptional { get; init; }
        public string? MinimumVersionOptionalMessage { get; init; }

        // The window the rule applies in; outside it the client should not enforce.
        public DateOnly? StartDate { get; init; }
        public DateOnly? EndDate { get; init; }
    }
}