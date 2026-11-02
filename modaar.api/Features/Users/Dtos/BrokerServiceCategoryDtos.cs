using FluentValidation;

namespace modaar.api.Features.Users.Dtos;

// Active services, for the marketplace filter and the broker's profile picker.
public record BrokerServiceCategoryDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? IconUrl { get; init; }
}

public record BrokerServiceCategoryAdminDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public string? IconUrl { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

public record CreateBrokerServiceCategoryDto
{
    public string Code { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string? IconUrl { get; init; }
    public int SortOrder { get; init; }
}

// Only what's sent changes. Code stays.
public record UpdateBrokerServiceCategoryDto
{
    public string? NameAr { get; init; }
    public string? NameEn { get; init; }
    public string? IconUrl { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class CreateBrokerServiceCategoryValidator : AbstractValidator<CreateBrokerServiceCategoryDto>
{
    public CreateBrokerServiceCategoryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[a-z0-9_]+$")
            .WithMessage("code is lower-case letters, digits and underscores.");
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.IconUrl).Must(BrokerProfileRules.BeUrl).MaximumLength(500).When(x => x.IconUrl is not null)
            .WithMessage("iconUrl must be an http(s) URL.");
    }
}

public sealed class UpdateBrokerServiceCategoryValidator : AbstractValidator<UpdateBrokerServiceCategoryDto>
{
    public UpdateBrokerServiceCategoryValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100).When(x => x.NameAr is not null);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100).When(x => x.NameEn is not null);
        RuleFor(x => x.IconUrl).Must(BrokerProfileRules.BeUrl).MaximumLength(500).When(x => x.IconUrl is not null)
            .WithMessage("iconUrl must be an http(s) URL.");
    }
}

// The broker's own profile. Only what's sent changes; a list sent replaces the whole list.
public record UpdateMyBrokerProfileDto
{
    public string? SubtitleAr { get; init; }
    public string? SubtitleEn { get; init; }
    public string? AboutAr { get; init; }
    public string? AboutEn { get; init; }

    // The year the brokerage started; years of experience are counted from it.
    public int? ExperienceSinceYear { get; init; }

    // Codes from GET /api/broker-services.
    public IReadOnlyList<string>? ServiceCodes { get; init; }

    public IReadOnlyList<BrokerCoverageAreaInputDto>? CoverageAreas { get; init; }
}

public record BrokerCoverageAreaInputDto
{
    public string NameAr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
}

public sealed class UpdateMyBrokerProfileValidator : AbstractValidator<UpdateMyBrokerProfileDto>
{
    public const int MaxCoverageAreas = 30;

    public UpdateMyBrokerProfileValidator()
    {
        RuleFor(x => x.SubtitleAr).MaximumLength(300);
        RuleFor(x => x.SubtitleEn).MaximumLength(300);
        RuleFor(x => x.AboutAr).MaximumLength(4000);
        RuleFor(x => x.AboutEn).MaximumLength(4000);
        RuleFor(x => x.ExperienceSinceYear!.Value)
            .InclusiveBetween(1950, DateTime.UtcNow.Year)
            .When(x => x.ExperienceSinceYear is not null)
            .WithName("experienceSinceYear");
        RuleFor(x => x.CoverageAreas!.Count).LessThanOrEqualTo(MaxCoverageAreas)
            .When(x => x.CoverageAreas is not null).WithName("coverageAreas");
        RuleForEach(x => x.CoverageAreas).ChildRules(a =>
        {
            a.RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
            a.RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        });
    }
}

internal static class BrokerProfileRules
{
    public static bool BeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
