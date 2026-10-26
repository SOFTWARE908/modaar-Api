using modaar.api.Features.Buildings.Entities;
using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Properties.Enums;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Features.Properties.Entities;

// A rentable unit. Deliberately holds nothing about who is renting it or for how much: the
// tenant, the rent actually being paid, the lease dates and the rented/vacant status all come
// from the active contract. Storing them here means two places to keep in step.
public class Property
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    // The brokerage managing it, once the owner has authorised one.
    public Guid? BrokerId { get; set; }

    // The building it is in. Null only on units added before buildings existed and deleted since;
    // the migration grouped every live unit into one.
    public Guid? BuildingId { get; set; }

    // Navigation, never lazy-loaded: Include it or project through it.
    public User Owner { get; set; } = null!;
    public Broker? Broker { get; set; }
    public Building? Building { get; set; }
    public List<Contract> Contracts { get; set; } = [];

    public string? UnitNumber { get; set; }

    // Text, not a number: "4", "G", "الأرضي", "M" all occur.
    public string? Floor { get; set; }

    public string Title { get; set; } = null!;

    public string? AddressLine { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public PropertyType PropertyType { get; set; }
    public int RoomsCount { get; set; }

    // Unit specs on the details screen. Null until the owner fills them in.
    public decimal? Area { get; set; }          // square metres
    public int? BathroomsCount { get; set; }

    // The asking rent, which is what a vacant unit is advertised at. What a sitting tenant
    // actually pays lives on their contract and can differ.
    public decimal AnnualRent { get; set; }

    // Shown on the list card; the first gallery image is a reasonable fallback.
    public string? CoverImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Display-only, always loaded with the property and replaced as a whole list, so a JSON
    // column rather than a table — same call as the broker's services and coverage areas.
    public List<PropertyImage> Images { get; set; } = [];
}

public class PropertyImage
{
    public string Url { get; set; } = null!;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}
