using FluentValidation;

namespace modaar.api.Features.Buildings.Dtos
{
    // "أنظمة المبنى": the categories this building has rules in (in the admin's order), each with
    // its rules in order, then the important numbers.
    public record BuildingRulesDto
    {
        public required Guid BuildingId { get; init; }
        public required string BuildingName { get; init; }

        // "آخر تحديث"; null until the first rule or number is added.
        public DateTimeOffset? UpdatedAt { get; init; }

        // Only whoever added the building may change them, same as editing the building.
        public required bool CanEdit { get; init; }

        // Categories with no rules in this building are left out.
        public required IReadOnlyList<BuildingRuleSectionDto> Sections { get; init; }

        // "أرقام مهمة", in the order they were added.
        public required IReadOnlyList<BuildingContactDto> Contacts { get; init; }
    }

    public record BuildingRuleSectionDto
    {
        public required Guid CategoryId { get; init; }
        public required string Code { get; init; }
        public required string Name { get; init; }
        public string? IconUrl { get; init; }
        public required IReadOnlyList<BuildingRuleDto> Rules { get; init; }
    }

    public record BuildingRuleDto
    {
        public required Guid Id { get; init; }
        public required string Text { get; init; }
    }

    public record BuildingContactDto
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public string? Note { get; init; }
        public required string PhoneNumber { get; init; }
        public required bool IsEmergency { get; init; }
    }

    // Added at the end of its category.
    public record AddBuildingRuleDto
    {
        public Guid CategoryId { get; init; }
        public string Text { get; init; } = null!;
    }

    // Added at the end of the list.
    public record AddBuildingContactDto
    {
        public string Name { get; init; } = null!;
        public string? Note { get; init; }
        public string PhoneNumber { get; init; } = null!;
        public bool IsEmergency { get; init; }
    }

    public sealed class AddBuildingRuleValidator : AbstractValidator<AddBuildingRuleDto>
    {
        public AddBuildingRuleValidator()
        {
            RuleFor(x => x.CategoryId).NotEmpty();
            RuleFor(x => x.Text).NotEmpty().MaximumLength(500);
        }
    }

    public sealed class AddBuildingContactValidator : AbstractValidator<AddBuildingContactDto>
    {
        public AddBuildingContactValidator()
        {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Note).MaximumLength(100);
            // Short codes like 911 too.
            RuleFor(x => x.PhoneNumber).NotEmpty().Matches(@"^\+?\d{3,15}$").WithMessage("phoneNumber is 3 to 15 digits, optionally starting with +.");
        }
    }

    // ── Categories (admin) ──

    public record BuildingRuleCategoryDto
    {
        public required Guid Id { get; init; }
        public required string Code { get; init; }
        public required string Name { get; init; }
        public string? IconUrl { get; init; }
    }

    public record BuildingRuleCategoryAdminDto
    {
        public required Guid Id { get; init; }
        public required string Code { get; init; }
        public required string NameAr { get; init; }
        public required string NameEn { get; init; }
        public string? IconUrl { get; init; }
        public required int SortOrder { get; init; }
        public required bool IsActive { get; init; }
    }

    public record CreateBuildingRuleCategoryDto
    {
        public string Code { get; init; } = string.Empty;
        public string NameAr { get; init; } = string.Empty;
        public string NameEn { get; init; } = string.Empty;
        public string? IconUrl { get; init; }
        public int SortOrder { get; init; }
    }

    // Only what's sent changes. Code stays.
    public record UpdateBuildingRuleCategoryDto
    {
        public string? NameAr { get; init; }
        public string? NameEn { get; init; }
        public string? IconUrl { get; init; }
        public int? SortOrder { get; init; }
        public bool? IsActive { get; init; }
    }

    public sealed class CreateBuildingRuleCategoryValidator : AbstractValidator<CreateBuildingRuleCategoryDto>
    {
        public CreateBuildingRuleCategoryValidator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[a-z0-9_]+$")
                .WithMessage("code is lower-case letters, digits and underscores.");
            RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
            RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
            RuleFor(x => x.IconUrl).Must(BuildingRuleCategoryRules.BeUrl).MaximumLength(500).When(x => x.IconUrl is not null)
                .WithMessage("iconUrl must be an http(s) URL.");
        }
    }

    public sealed class UpdateBuildingRuleCategoryValidator : AbstractValidator<UpdateBuildingRuleCategoryDto>
    {
        public UpdateBuildingRuleCategoryValidator()
        {
            RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100).When(x => x.NameAr is not null);
            RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100).When(x => x.NameEn is not null);
            RuleFor(x => x.IconUrl).Must(BuildingRuleCategoryRules.BeUrl).MaximumLength(500).When(x => x.IconUrl is not null)
                .WithMessage("iconUrl must be an http(s) URL.");
        }
    }

    internal static class BuildingRuleCategoryRules
    {
        public static bool BeUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
