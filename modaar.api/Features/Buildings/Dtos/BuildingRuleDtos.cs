using FluentValidation;

namespace modaar.api.Features.Buildings.Dtos
{
    // "أنظمة المبنى": sections in display order, each with its rules in order.
    public record BuildingRulesDto
    {
        public required Guid BuildingId { get; init; }
        public required string BuildingName { get; init; }

        // "آخر تحديث"; null while the building has no rules yet (sections is empty then).
        public DateTimeOffset? UpdatedAt { get; init; }

        // Only whoever added the building may change them, same as editing the building.
        public required bool CanEdit { get; init; }

        public required IReadOnlyList<BuildingRuleSectionDto> Sections { get; init; }
    }

    public record BuildingRuleSectionDto
    {
        public required Guid Id { get; init; }
        public required string Title { get; init; }
        public required IReadOnlyList<BuildingRuleDto> Rules { get; init; }
    }

    public record BuildingRuleDto
    {
        public required Guid Id { get; init; }
        public required string Text { get; init; }
    }

    // Added at the end of the list.
    public record AddBuildingRuleSectionDto
    {
        public string Title { get; init; } = null!;
    }

    // Added at the end of its section.
    public record AddBuildingRuleDto
    {
        public string Text { get; init; } = null!;
    }

    public sealed class AddBuildingRuleSectionValidator : AbstractValidator<AddBuildingRuleSectionDto>
    {
        public AddBuildingRuleSectionValidator() => RuleFor(x => x.Title).NotEmpty().MaximumLength(100);
    }

    public sealed class AddBuildingRuleValidator : AbstractValidator<AddBuildingRuleDto>
    {
        public AddBuildingRuleValidator() => RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
    }
}
