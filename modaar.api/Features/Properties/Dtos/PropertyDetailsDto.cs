using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Properties.Dtos;

// GET /properties/{id} — one call, everything the owner's property screen renders.
public record PropertyDetailsDto
{
    public required PropertySummaryDto Summary { get; init; }

    // Null when the unit is vacant.
    public PropertyTenantContractDto? TenantContract { get; init; }

    // Null until the owner authorises a brokerage.
    public BrokerSummaryDto? Broker { get; init; }

    public required IReadOnlyList<string> ImageUrls { get; init; }

    // From the handover record for the current tenancy; empty and null when there isn't one.
    public required IReadOnlyList<string> HandoverImageUrls { get; init; }
    public string? HandoverDescription { get; init; }

    // The rent card: the installment that matters now. Null with no active lease or no schedule.
    public PropertyCurrentRentDto? CurrentRent { get; init; }

    // When the last lease ended, for "شاغرة منذ …". Null while rented or if never rented.
    public DateOnly? VacantSince { get; init; }

    // Every lease on the unit, newest first ("كل العقود").
    public required IReadOnlyList<PropertyContractHistoryItemDto> Contracts { get; init; }

    public required IReadOnlyList<PropertyMaintenanceHistoryItemDto> MaintenanceHistory { get; init; }
}

// The tenancy block: who is in the unit and on what terms.
public record PropertyTenantContractDto
{
    public required Guid ContractId { get; init; }
    public required string ContractNumber { get; init; }

    public required Guid TenantUserId { get; init; }
    public string? TenantName { get; init; }
    public string? TenantAvatarUrl { get; init; }

    // "+9665…", for the call button on the tenant card.
    public string? TenantPhoneNumber { get; init; }

    // Secondary line under the name, e.g. how long they have been a tenant.
    public string? TenantSubtitle { get; init; }

    // The enum, not a rendered label: the client already localizes statuses elsewhere and
    // shipping a translated string here would be the only place we don't.
    public required ContractStatus Status { get; init; }

    public required DateOnly ContractStart { get; init; }
    public required DateOnly ContractEnd { get; init; }

    // Negative once the lease has run out, so the UI can distinguish "ends soon" from "overdue"
    // without doing date maths.
    public required int RemainingDays { get; init; }

    public required decimal InstallmentAmount { get; init; }
    public required string Currency { get; init; }

    public required ContractRenewalAvailability RenewalAvailability { get; init; }
}

// A row in the unit's work history — closed requests, not the live queue.
public record PropertyMaintenanceHistoryItemDto
{
    public required Guid RequestId { get; init; }
    public required string RequestNumber { get; init; }

    public required string Title { get; init; }
    public string? Description { get; init; }

    public required MaintenanceServiceTypeDto ServiceType { get; init; }
    public required MaintenanceRequestStatus Status { get; init; }

    public required DateTimeOffset Date { get; init; }

    // The quote total once priced; null before.
    public decimal? CostAmount { get; init; }
    public string? Currency { get; init; }

    // Same meaning as on the maintenance list.
    public required MaintenanceStage Stage { get; init; }
    public string? NextActionLabel { get; init; }
    public string? TenantName { get; init; }
}

public record PropertyCurrentRentDto
{
    public required Guid PaymentId { get; init; }

    // "سبتمبر 2026" / "September 2026", from the period the installment covers.
    public required string PeriodLabel { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }

    // paid · due · overdue · underReview
    public required PaymentDisplayStatus Status { get; init; }
    public DateOnly? DueDate { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public required int DaysOverdue { get; init; }
}

public record PropertyContractHistoryItemDto
{
    public required Guid ContractId { get; init; }
    public required string ContractNumber { get; init; }
    public string? TenantName { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required ContractStatus Status { get; init; }
}
