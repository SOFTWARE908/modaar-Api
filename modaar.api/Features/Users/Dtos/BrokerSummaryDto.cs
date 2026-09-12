namespace modaar.api.Features.Users.Dtos;

// The compact broker block embedded in property details and the dashboards. Name and logo come
// from the broker's User row; the subtitle is the brokerage's own.
public record BrokerSummaryDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? LogoUrl { get; init; }
    public string? Subtitle { get; init; }

    public required decimal RatingAverage { get; init; }
    public required int ReviewsCount { get; init; }
    public required bool IsVerified { get; init; }
}
