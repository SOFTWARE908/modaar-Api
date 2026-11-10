using modaar.api.Common.Dtos;
using modaar.api.Features.Payments.Enums;

namespace modaar.api.Features.Payments.Dtos;

public record PaymentListQueryDto : PagedRequestDto
{
    public PaymentType? Type { get; init; }
    public bool IncludeUpcoming { get; init; }

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}
