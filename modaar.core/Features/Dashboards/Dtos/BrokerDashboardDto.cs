using modaar.api.Features.Maintenance.Dtos;

namespace modaar.api.Features.Dashboards.Dtos;

public record BrokerDashboardDto
{
    public string? UserName { get; init; }

    public required BrokerDealsSummaryDto ClosedDeals { get; init; }
    public required IReadOnlyList<DashboardStatDto> Stats { get; init; }

    // Across the managed properties: rent paid this month, and rent due this month.
    public required decimal Collected { get; init; }
    public required decimal Expected { get; init; }

    // needsQuote · readyForTechnician · newRequests · lateRents (count + amount)
    public required IReadOnlyList<DashboardTaskDto> Tasks { get; init; }

    public required IReadOnlyList<MaintenanceRequestListItemDto> MaintenancePreview { get; init; }
    public required IReadOnlyList<BrokerClientDto> ClientsPreview { get; init; }
    public required IReadOnlyList<TransactionPreviewDto> TransactionsPreview { get; init; }
}
