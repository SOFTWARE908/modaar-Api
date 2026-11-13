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

    // The Saudi national address in parts (العنوان الوطني), filled by the admin panel. The app still
    // reads AddressLine/District/City, which the panel fills from these, so it shows one address
    // as before. Buildings an owner added from the app have none of these.
    public string? BuildingNumber { get; set; }     // 4 digits
    public string? Street { get; set; }
    public string? AdditionalNumber { get; set; }   // الرقم الإضافي, 4 digits
    public string? PostalCode { get; set; }         // 5 digits

    // The city picked from the Cities list. City above keeps its Arabic name for the app.
    public Guid? CityId { get; set; }

    // Navigation, never lazy-loaded: Include it or project through it. Not named City because the
    // City text column above already has that name.
    public City? CityLookup { get; set; }

    // رقم الصك from the real-estate registry, as typed. Not verified until the registry is wired in.
    public string? DeedNumber { get; set; }
    // As written on the deed, Hijri yyyy-MM-dd (e.g. 1445-11-12). Not converted.
    public string? DeedIssueDateHijri { get; set; }
    public DeedType? DeedType { get; set; }
    public int? YearBuilt { get; set; }

    // Building specs from the Ejar sheet. Free text where the sheet gives no list of options.
    public int? FloorsCount { get; set; }
    // Typed by the admin: how many units the building holds. The panel adds no more units than this.
    public int? UnitsCount { get; set; }
    public string? UsagePurpose { get; set; }       // الغرض من الاستخدام, e.g. "سكن عائلات"
    public int? ParkingSpaces { get; set; }

    // "آخر تحديث" on the rules screen. Null until rules are first saved.
    public DateTimeOffset? RulesUpdatedAt { get; set; }

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

// نوع الصك, the options on the Ejar sheet.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeedType
{
    // صك ملكية إلكتروني
    Electronic,
    // صك ملكية ورقي
    Paper,
    // حجة استحكام
    HujjatIstihkam,
    // صك السجل العقاري
    RealEstateRegistry,
    // أخرى
    Other
}

// A city to pick from (the Ejar sheet's list). Seeded; managed by the admin later.
public class City
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
