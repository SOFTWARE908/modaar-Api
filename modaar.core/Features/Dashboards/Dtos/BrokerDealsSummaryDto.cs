namespace modaar.api.Features.Dashboards.Dtos;

public record BrokerDealsSummaryDto
{
    public required int Count { get; init; }

    // Which window the count covers, e.g. "This month".
    public required string PeriodLabel { get; init; }
}
