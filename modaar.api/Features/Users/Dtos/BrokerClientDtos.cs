using modaar.api.Common.Dtos;
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

    public required OwnerSubscriptionPlan Plan { get; init; }
    public decimal? AutoApproveLimit { get; init; }
}

// Client screen (B-6).
public record BrokerClientDetailsDto
{
    public required Guid OwnerUserId { get; init; }
    public string? Name { get; init; }
    public required string PhoneNumber { get; init; }
    public string? ImageUrl { get; init; }

    // When the owner first accepted this broker's management. Null for clients linked before
    // management requests existed.
    public DateTimeOffset? OwnerSince { get; init; }

    public required OwnerSubscriptionPlan Plan { get; init; }
    public decimal? AutoApproveLimit { get; init; }

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
}
