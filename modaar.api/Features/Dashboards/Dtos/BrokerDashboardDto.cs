using modaar.api.Features.Maintenance.Dtos;

namespace modaar.api.Features.Dashboards.Dtos;

public record BrokerDashboardDto
{
    public string? UserName { get; init; }

    public required BrokerDealsSummaryDto ClosedDeals { get; init; }
    public required IReadOnlyList<DashboardStatDto> Stats { get; init; }

    public required IReadOnlyList<MaintenanceRequestListItemDto> MaintenancePreview { get; init; }
    public required IReadOnlyList<BrokerClientDto> ClientsPreview { get; init; }
    public required IReadOnlyList<TransactionPreviewDto> TransactionsPreview { get; init; }
}
