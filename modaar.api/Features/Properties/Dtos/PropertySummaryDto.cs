using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Properties.Dtos;

// The header block on the property details screen: the unit itself, nothing about the tenancy
// beyond whether it is occupied.
public record PropertySummaryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? ImageUrl { get; init; }

    // Pre-composed for display — district, city. The parts stay on the entity for anyone who
    // needs them separately.
    public string? Address { get; init; }

    public required PropertyType PropertyType { get; init; }
    public required int RoomsCount { get; init; }

    public required decimal AnnualRent { get; init; }
    public required string Currency { get; init; }

    public required PropertyRentalStatus Status { get; init; }

    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
}
