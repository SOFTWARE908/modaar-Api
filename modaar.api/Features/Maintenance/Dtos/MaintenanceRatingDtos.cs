using FluentValidation;
using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance.Dtos
{
    public record MaintenanceRatingSubmitDto
    {
        public required int Stars { get; init; }

        public IReadOnlyList<string>? TagKeys { get; init; }
        public string? Comment { get; init; }

        // The UI offers 10 / 20 / 50 / 100 SAR as buttons, or a free amount. Both end up in one
        // column on the entity; only one of the two may be sent.
        public int? PresetTipSar { get; init; }
        public decimal? CustomTipSar { get; init; }
    }

    public record NeedHelpRequestDto
    {
        public required NeedHelpIssueType IssueType { get; init; }
        public string? Description { get; init; }
        public required NeedHelpContactMethod ContactMethod { get; init; }
    }
}

namespace modaar.api.Features.Maintenance.Validation
{
    using modaar.api.Features.Maintenance.Dtos;

    public sealed class MaintenanceRatingSubmitValidator : AbstractValidator<MaintenanceRatingSubmitDto>
    {
        // Fixed list from the client. An unknown key would render as raw text in the app, so
        // reject rather than store it.
        private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
        {
            "maintenance_rating.tag_comfortable",
            "maintenance_rating.tag_great_personality",
            "maintenance_rating.tag_clean_comfortable",
            "maintenance_rating.tag_good_communication",
            "maintenance_rating.tag_excellent_service",
            "maintenance_rating.tag_respectful"
        };

        private static readonly int[] PresetTips = [10, 20, 50, 100];

        public MaintenanceRatingSubmitValidator()
        {
            RuleFor(x => x.Stars).InclusiveBetween(1, 5);

            RuleFor(x => x.Comment).MaximumLength(1000);

            RuleFor(x => x.TagKeys!)
                .Must(t => t.Count <= AllowedTags.Count)
                .Must(t => t.Distinct().Count() == t.Count).WithMessage("Tags must be unique.")
                .Must(t => t.All(AllowedTags.Contains)).WithMessage("One or more tags are not recognised.")
                .When(x => x.TagKeys is not null);

            RuleFor(x => x.PresetTipSar!.Value)
                .Must(PresetTips.Contains)
                .When(x => x.PresetTipSar is not null)
                .WithMessage("A preset tip must be one of 10, 20, 50 or 100 SAR.");

            RuleFor(x => x.CustomTipSar!.Value)
                .GreaterThan(0).LessThanOrEqualTo(5000)
                .When(x => x.CustomTipSar is not null);

            RuleFor(x => x)
                .Must(x => x.PresetTipSar is null || x.CustomTipSar is null)
                .WithMessage("Send either a preset tip or a custom tip, not both.");
        }
    }

    public sealed class NeedHelpRequestValidator : AbstractValidator<NeedHelpRequestDto>
    {
        public NeedHelpRequestValidator()
        {
            RuleFor(x => x.IssueType).IsInEnum();
            RuleFor(x => x.ContactMethod).IsInEnum();
            RuleFor(x => x.Description).MaximumLength(2000);
        }
    }
}
