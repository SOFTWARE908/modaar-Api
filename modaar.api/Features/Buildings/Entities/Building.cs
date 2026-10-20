using System.Text.Json.Serialization;

namespace modaar.api.Features.Buildings.Entities;

// A building (or compound) that units belong to — "برج النخيل" holding 101, 102, …
//
// A building has no single owner: units inside it can belong to different owners, each a
// Property with its own OwnerUserId. So who sees a building is decided by its units — an owner
// sees the buildings their units are in, a broker those holding units they manage — and every
// figure on it is computed over the caller's own units only.
public class Building
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public BuildingType Type { get; set; }

    public string? AddressLine { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public string? CoverImageUrl { get; set; }

    // Who added it. The only one who may edit its name and address, since others may share it.
    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BuildingType
{
    // عمارة سكنية
    ResidentialBuilding,
    // مجمع فلل
    VillaCompound,
    Other
}
