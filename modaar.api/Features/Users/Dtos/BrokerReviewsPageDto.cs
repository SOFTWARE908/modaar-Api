using modaar.api.Common.Dtos;

namespace modaar.api.Features.Users.Dtos;

// A page of reviews plus the flag the client needs to decide between offering a
// "write a review" button and an edit.
public record BrokerReviewsPageDto : PagedResultDto<BrokerReviewDto>
{
    public required bool IsReviewedByMe { get; init; }
}
