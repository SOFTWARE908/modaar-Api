namespace modaar.api.Features.Users.Dtos;

public record BrokerReviewDto
{
    public required Guid Id { get; init; }

    // Null when the reviewer never completed their profile.
    public string? ReviewerName { get; init; }
    public string? ReviewerImageUrl { get; init; }

    // The rounded average of the four criteria.
    public required int Rating { get; init; }
    public required BrokerReviewCriteriaDto Criteria { get; init; }
    public string? Comment { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

// Stars per criterion: whole numbers on one review, averages (1 decimal) on the profile.
public record BrokerReviewCriteriaDto
{
    public required decimal Responsiveness { get; init; }   // سرعة الاستجابة
    public required decimal Management { get; init; }       // إدارة العقار والمستأجرين
    public required decimal Transparency { get; init; }     // الشفافية والتقارير
    public required decimal Marketing { get; init; }        // تسويق الوحدات الشاغرة
}
