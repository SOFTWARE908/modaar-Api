namespace modaar.api.Features.Users.Dtos;

public record BrokerReviewDto
{
    public required Guid Id { get; init; }

    // Null when the reviewer never completed their profile.
    public string? ReviewerName { get; init; }
    public string? ReviewerImageUrl { get; init; }

    public required int Rating { get; init; }
    public string? Comment { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
