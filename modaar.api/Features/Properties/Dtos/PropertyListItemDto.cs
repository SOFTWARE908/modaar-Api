using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Properties.Dtos;

// A card in the owner's "My properties" list. Everything below the address line is read off the
// active contract, so all of it is null for a vacant unit.
public record PropertyListItemDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? ImageUrl { get; init; }

    public required PropertyRentalStatus Status { get; init; }

    // What the sitting tenant pays. For a vacant unit this is null and the card should fall back
    // to AnnualRent / 12 as the asking price.
    public decimal? MonthlyRent { get; init; }
    public required decimal AnnualRent { get; init; }
    public required string Currency { get; init; }

    public string? TenantName { get; init; }

    public DateOnly? LeaseStart { get; init; }
    public DateOnly? LeaseEnd { get; init; }

    // 0.0–1.0, how much of the lease has elapsed. Computed server-side so the progress bar isn't
    // at the mercy of the device clock.
    public double? RentalProgress { get; init; }
}
