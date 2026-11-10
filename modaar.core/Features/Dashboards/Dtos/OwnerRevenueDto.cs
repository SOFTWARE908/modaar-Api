namespace modaar.api.Features.Dashboards.Dtos;

public record OwnerRevenueDto
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }

    // Change against the previous period. Null while there is nothing to compare against.
    public decimal? TrendPercent { get; init; }
}
