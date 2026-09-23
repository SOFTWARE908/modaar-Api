namespace modaar.api.Features.Users.Dtos;

// The public brokerage profile. Name and logo come from the broker's User row; everything else
// from the Brokers extension row.
public record BrokerDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? LogoUrl { get; init; }
    public string? Subtitle { get; init; }

    public required decimal Rating { get; init; }
    public required int ReviewsCount { get; init; }
    public required bool IsVerified { get; init; }

    public string? LicenseNumber { get; init; }
    public string? About { get; init; }

    // Localized by Accept-Language, flattened to plain strings for the client.
    public required IReadOnlyList<string> Services { get; init; }
    public required IReadOnlyList<string> CoverageAreas { get; init; }

    public required IReadOnlyList<BrokerStatDto> Stats { get; init; }

    // First page only, as a preview. The full list is the reviews endpoint.
    public required IReadOnlyList<BrokerReviewDto> Reviews { get; init; }
}
