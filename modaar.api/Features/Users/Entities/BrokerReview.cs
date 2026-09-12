namespace modaar.api.Features.Users.Entities
{
    // A real table rather than an owned JSON collection: it is paged, it points at a user, and
    // one reviewer may leave at most one review per brokerage.
    //
    // Broker.RatingAverage and Broker.ReviewsCount are a rollup over these rows, counting only
    // the published ones. They are recalculated from scratch on every write rather than nudged,
    // so an edited rating, an unpublish and a delete all land on the same correct number.
    public class BrokerReview
    {
        public Guid Id { get; set; }

        public Guid BrokerId { get; set; }
        public Broker Broker { get; set; } = null!;

        public Guid ReviewerUserId { get; set; }
        public User Reviewer { get; set; } = null!;

        // Whole stars, 1–5. The average on Broker is decimal(3,2), so 4 and 5 roll up to 4.50
        // rather than truncating to 4.
        public int Rating { get; set; }

        public string? Comment { get; set; }

        // Lets a review be pulled from the public profile without deleting it; unpublished
        // reviews are excluded from both rollup columns.
        public bool IsPublished { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
