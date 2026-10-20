using System.Text.Json.Serialization;

namespace modaar.api.Features.Dashboards.Dtos;

// One line in the "tasks" card. The app writes the text; the server only counts.
public record DashboardTaskDto
{
    public required DashboardTaskType Type { get; init; }
    public required int Count { get; init; }

    // Set where a total means something, e.g. late rents: "3 · 12,500 ر.س".
    public decimal? Amount { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DashboardTaskType
{
    // Maintenance, counted from the same rule as each request's availableActions, so the number
    // always matches the buttons the user will find.
    AwaitingOwnerApproval,
    NewRequests,
    NeedsQuote,
    ReadyForTechnician,

    // Active leases ending within 60 days.
    ContractsExpiring,

    // Unpaid rent past its due date.
    LateRents
}
