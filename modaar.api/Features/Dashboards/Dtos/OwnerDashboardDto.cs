using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Maintenance.Dtos;

namespace modaar.api.Features.Dashboards.Dtos;

public record OwnerDashboardDto
{
    // Null until the profile is completed.
    public string? UserName { get; init; }

    public required OwnerRevenueDto Revenue { get; init; }
    public required IReadOnlyList<DashboardStatDto> Stats { get; init; }

    // The brokerage managing the owner's portfolio. Null when none is assigned — the spec marks
    // this required, which cannot hold for an owner managing their own units.
    public BrokerSummaryDto? Broker { get; init; }

    public required IReadOnlyList<MaintenanceRequestListItemDto> MaintenancePreview { get; init; }
    public required IReadOnlyList<TransactionPreviewDto> TransactionsPreview { get; init; }
}
