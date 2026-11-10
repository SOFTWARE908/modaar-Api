using FluentValidation;
using modaar.api.Common.Dtos;
using modaar.api.Features.Announcements.Entities;

namespace modaar.api.Features.Announcements.Dtos;

// ── Input ──────────────────────────────────────────────────────────────────

// Owner. Scope picks the units: Building → buildingId; Units → propertyIds; AllProperties → none.
public record SendAnnouncementDto
{
    public Guid CategoryId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTimeOffset? StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }
    public string? ImageUrl { get; init; }

    public AnnouncementScope Scope { get; init; }
    public Guid? BuildingId { get; init; }
    public IReadOnlyList<Guid>? PropertyIds { get; init; }
}

public record CreateAnnouncementCategoryDto
{
    public string Code { get; init; } = string.Empty;
    public string NameAr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

// Only what's sent changes. Code stays: the app keys icons off it.
public record UpdateAnnouncementCategoryDto
{
    public string? NameAr { get; init; }
    public string? NameEn { get; init; }
    public int? SortOrder { get; init; }
    public bool? IsActive { get; init; }
}

// ── Output ─────────────────────────────────────────────────────────────────

public record AnnouncementCategoryDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string Name { get; init; }
}

public record AnnouncementCategoryAdminDto
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required string NameAr { get; init; }
    public required string NameEn { get; init; }
    public required int SortOrder { get; init; }
    public required bool IsActive { get; init; }
}

// Tenant's inbox row and screen.
public record AnnouncementDto
{
    public required Guid Id { get; init; }
    public required AnnouncementCategoryDto Category { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset? StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }
    public string? ImageUrl { get; init; }

    public string? SenderName { get; init; }
    public string? SenderImageUrl { get; init; }

    // The tenant's unit it came for.
    public required string PropertyTitle { get; init; }
    public string? UnitNumber { get; init; }
    public string? BuildingName { get; init; }

    public DateTimeOffset? AcknowledgedAt { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

// Owner's sent list.
public record SentAnnouncementDto
{
    public required Guid Id { get; init; }
    public required AnnouncementCategoryDto Category { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public DateTimeOffset? StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; init; }
    public string? ImageUrl { get; init; }

    public required AnnouncementScope Scope { get; init; }
    public Guid? BuildingId { get; init; }
    public string? BuildingName { get; init; }
    public required int UnitsCount { get; init; }

    public required int RecipientsCount { get; init; }
    public required int AcknowledgedCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public record SentAnnouncementDetailsDto : SentAnnouncementDto
{
    public required IReadOnlyList<AnnouncementRecipientDto> Recipients { get; init; }
}

public record AnnouncementRecipientDto
{
    public required Guid TenantUserId { get; init; }
    public string? Name { get; init; }
    public string? ImageUrl { get; init; }
    public required string PropertyTitle { get; init; }
    public string? UnitNumber { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
}

// ── Validation ─────────────────────────────────────────────────────────────

public sealed class SendAnnouncementValidator : AbstractValidator<SendAnnouncementDto>
{
    public SendAnnouncementValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Scope).IsInEnum();

        RuleFor(x => x.EndsAt).GreaterThanOrEqualTo(x => x.StartsAt!.Value)
            .When(x => x.StartsAt is not null && x.EndsAt is not null)
            .WithMessage("endsAt can't be before startsAt.");

        RuleFor(x => x.ImageUrl)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .MaximumLength(500)
            .When(x => x.ImageUrl is not null)
            .WithMessage("imageUrl must be an http(s) URL.");

        RuleFor(x => x.BuildingId).NotEmpty().When(x => x.Scope == AnnouncementScope.Building)
            .WithMessage("buildingId is required when scope is building.");
        RuleFor(x => x.PropertyIds).NotEmpty().When(x => x.Scope == AnnouncementScope.Units)
            .WithMessage("propertyIds is required when scope is units.");
        RuleFor(x => x.PropertyIds!.Count).LessThanOrEqualTo(200).When(x => x.PropertyIds is not null);
    }
}

public sealed class CreateAnnouncementCategoryValidator : AbstractValidator<CreateAnnouncementCategoryDto>
{
    public CreateAnnouncementCategoryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50).Matches("^[a-z0-9_]+$")
            .WithMessage("code is lower-case letters, digits and underscores.");
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateAnnouncementCategoryValidator : AbstractValidator<UpdateAnnouncementCategoryDto>
{
    public UpdateAnnouncementCategoryValidator()
    {
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100).When(x => x.NameAr is not null);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100).When(x => x.NameEn is not null);
    }
}
