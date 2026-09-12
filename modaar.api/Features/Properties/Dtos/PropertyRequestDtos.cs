using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Properties.Dtos;

public record CreatePropertyRequestDto
{
    public required string Title { get; init; }

    public required PropertyType PropertyType { get; init; }
    public required int RoomsCount { get; init; }

    // The asking rent. What a tenant ends up paying is set on their contract.
    public required decimal AnnualRent { get; init; }

    public string? AddressLine { get; init; }
    public string? District { get; init; }
    public string? City { get; init; }

    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    public string? CoverImageUrl { get; init; }

    public IReadOnlyList<PropertyImageDto>? Images { get; init; }
}

// Partial update: an omitted field is left alone. Images are the exception — see the note on
// the property.
public record UpdatePropertyRequestDto
{
    public string? Title { get; init; }

    public PropertyType? PropertyType { get; init; }
    public int? RoomsCount { get; init; }
    public decimal? AnnualRent { get; init; }

    public string? AddressLine { get; init; }
    public string? District { get; init; }
    public string? City { get; init; }

    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    public string? CoverImageUrl { get; init; }

    // Replaces the whole gallery when present, because the list is stored as one JSON column and
    // there is no per-image id to patch against. Omit it to leave the gallery untouched; send an
    // empty array to clear it.
    public IReadOnlyList<PropertyImageDto>? Images { get; init; }
}

public record PropertyImageDto
{
    public required string Url { get; init; }
    public string? Caption { get; init; }
    public int SortOrder { get; init; }
}
