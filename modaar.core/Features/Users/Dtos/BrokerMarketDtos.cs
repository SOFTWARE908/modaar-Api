using System.Text.Json.Serialization;
using FluentValidation;
using modaar.api.Common.Dtos;

namespace modaar.api.Features.Users.Dtos;

// "سوق الوسطاء": every brokerage, for an owner choosing one.
public record BrokersRequestDto : PagedRequestDto
{
    // Matches the brokerage's name, or a district/city it covers or runs units in.
    public string? Search { get; init; }

    public BrokerSort Sort { get; init; } = BrokerSort.Rating;

    // A code from GET /api/broker-services: only brokerages offering it.
    public string? Service { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BrokerSort
{
    // Highest rated first (more reviews break ties).
    Rating,
    // Most units run now first.
    Units
}

public record BrokerListItemDto
{
    public required Guid Id { get; init; }

    // The account id, for chat.
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public string? LogoUrl { get; init; }
    public string? Subtitle { get; init; }

    public required decimal Rating { get; init; }
    public required int ReviewsCount { get; init; }
    public required bool IsVerified { get; init; }

    public required int ManagedUnits { get; init; }
    public int? YearsOfExperience { get; init; }

    // Localized, display only.
    public required IReadOnlyList<string> Services { get; init; }
    public required IReadOnlyList<string> CoverageAreas { get; init; }
}

// The owner's review: four criteria and a comment. Sending again edits it.
public record SubmitBrokerReviewDto
{
    public int Responsiveness { get; init; }
    public int Management { get; init; }
    public int Transparency { get; init; }
    public int Marketing { get; init; }
    public string? Comment { get; init; }
}

public sealed class SubmitBrokerReviewValidator : AbstractValidator<SubmitBrokerReviewDto>
{
    public SubmitBrokerReviewValidator()
    {
        RuleFor(x => x.Responsiveness).InclusiveBetween(1, 5);
        RuleFor(x => x.Management).InclusiveBetween(1, 5);
        RuleFor(x => x.Transparency).InclusiveBetween(1, 5);
        RuleFor(x => x.Marketing).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
