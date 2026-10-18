using FluentValidation;
using modaar.api.Features.Maintenance.Dtos;

namespace modaar.api.Features.Maintenance.Validation
{
    public sealed class MaintenanceRatingSubmitValidator : AbstractValidator<MaintenanceRatingSubmitDto>
    {
        private static readonly int[] PresetTips = [10, 20, 50, 100];

        // Generous ceiling; whether each key exists is checked against the tags table in the
        // service, since a validator has no database.
        private const int MaxTags = 20;

        public MaintenanceRatingSubmitValidator()
        {
            // All four or none: a partial set would make the average mean something different
            // from one rating to the next.
            RuleFor(x => x)
                .Must(x => x.TechnicianRating is not null && x.RepairQualityRating is not null &&
                           x.ResponseSpeedRating is not null && x.BrokerCommunicationRating is not null)
                .When(x => x.HasCriteria)
                .WithMessage("Send all four ratings: technician, repair quality, response speed and broker communication.");

            RuleFor(x => x.Stars)
                .NotNull()
                .When(x => !x.HasCriteria)
                .WithMessage("Send the four ratings, or stars.");

            RuleFor(x => x.TechnicianRating).InclusiveBetween(1, 5).When(x => x.TechnicianRating is not null);
            RuleFor(x => x.RepairQualityRating).InclusiveBetween(1, 5).When(x => x.RepairQualityRating is not null);
            RuleFor(x => x.ResponseSpeedRating).InclusiveBetween(1, 5).When(x => x.ResponseSpeedRating is not null);
            RuleFor(x => x.BrokerCommunicationRating).InclusiveBetween(1, 5).When(x => x.BrokerCommunicationRating is not null);
            RuleFor(x => x.Stars).InclusiveBetween(1, 5).When(x => x.Stars is not null);

            RuleFor(x => x.Comment).MaximumLength(1000);

            RuleFor(x => x.TagKeys!)
                .Must(t => t.Count <= MaxTags).WithMessage($"At most {MaxTags} tags are allowed.")
                .Must(t => t.Distinct().Count() == t.Count).WithMessage("Tags must be unique.")
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
