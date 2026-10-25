using modaar.api.Common.Dtos;
using modaar.api.Features.Agreements.Dtos;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Users.Dtos;

// "عملائي" (B-5): the owners whose units this broker manages. Every count and amount is over
// this broker's units only, not everything the owner has.
public record BrokerClientsRequestDto : PagedRequestDto
{
    // Matches the owner's name or phone number.
    public string? Search { get; init; }
}

public record BrokerClientListItemDto
{
    public required Guid OwnerUserId { get; init; }
    public string? Name { get; init; }
    public required string PhoneNumber { get; init; }
    public string? ImageUrl { get; init; }

    // Where most of their units are.
    public string? City { get; init; }

    // Buildings, then units inside them.
    public required int PropertiesCount { get; init; }
    public required int TotalUnits { get; init; }
    public required int RentedUnits { get; init; }
    public required int VacantUnits { get; init; }

    // Rent per month of the active leases (annual rent / 12, or the installment spread over its period).
    public required decimal MonthlyIncome { get; init; }

    // The relationship as a whole: pending (waiting for the owner), active, endingSoon (every
    // unit still held leaves within 30 days) or expired.
    public required AgreementDisplayStatus MandateStatus { get; init; }

    // Units leaving within 30 days, even while the relationship as a whole carries on.
    public required int UnitsEndingSoon { get; init; }

    // Management fee of the current agreement.
    public decimal? CommissionPercent { get; init; }
}

// Client screen (B-6).
public record BrokerClientDetailsDto
{
    public required Guid OwnerUserId { get; init; }
    public string? Name { get; init; }
    public required string PhoneNumber { get; init; }
    public string? ImageUrl { get; init; }

    // Start of the first agreement between them.
    public DateOnly? OwnerSince { get; init; }

    public required AgreementDisplayStatus MandateStatus { get; init; }
    public required int UnitsEndingSoon { get; init; }

    // The current agreement (the latest active one, else the latest that ended). Null if there
    // has never been one.
    public BrokerClientMandateDto? Mandate { get; init; }

    // Waiting for the owner: a first agreement or a renewal.
    public BrokerClientPendingMandateDto? PendingMandate { get; init; }

    public required int TotalUnits { get; init; }
    public required int RentedUnits { get; init; }
    public required int VacantUnits { get; init; }
    public required int OccupancyPercent { get; init; }
    public required decimal MonthlyIncome { get; init; }

    // This calendar month (Riyadh): paid during the month / rent installments due in it.
    public required decimal CollectedThisMonth { get; init; }
    public required decimal ExpectedThisMonth { get; init; }

    // Pending or accepted requests on these units.
    public required int OpenMaintenanceCount { get; init; }

    public required IReadOnlyList<BrokerClientPropertyDto> Properties { get; init; }
}

// A building, counted over this broker's units of this owner.
public record BrokerClientPropertyDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public required int TotalUnits { get; init; }
    public required int RentedUnits { get; init; }
    public required int OccupancyPercent { get; init; }

    // When the first of these units leaves the agreement. Null if none has a lease to leave with.
    public DateOnly? EndsOn { get; init; }

    // This owner's subscription in this building (default managed when never set).
    public required OwnerSubscriptionPlan Plan { get; init; }
    public decimal? AutoApproveLimit { get; init; }
}

public record BrokerClientMandateDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required AgreementDisplayStatus Status { get; init; }
    public DateOnly? StartDate { get; init; }

    // When the last unit leaves (null while a vacant unit has no lease yet), or the day it ended.
    public DateOnly? EndDate { get; init; }
    public int? DaysLeft { get; init; }
    public decimal? ManagementFeePercent { get; init; }
}

public record BrokerClientPendingMandateDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required bool IsRenewal { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
