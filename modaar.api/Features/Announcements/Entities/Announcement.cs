using System.Text.Json.Serialization;

namespace modaar.api.Features.Announcements.Entities;

// An owner's notice to their own tenants: elevator maintenance, stairwell cleaning, painting...
// Sent once, never edited. Recipients are fixed when it is sent: tenants with an active lease on
// the owner's units in scope at that moment.
public class Announcement
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public Guid CategoryId { get; set; }

    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    // When the thing happens, if it has a time ("Thursday 9–12").
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }

    public string? ImageUrl { get; set; }

    public AnnouncementScope Scope { get; set; }

    // Scope = Building.
    public Guid? BuildingId { get; set; }

    // Scope = Units. Stored as JSON: read and written whole, never joined against.
    public List<Guid> PropertyIds { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public List<AnnouncementRecipient> Recipients { get; set; } = [];
}

// One per tenant, even if they rent several of the units in scope.
public class AnnouncementRecipient
{
    public Guid Id { get; set; }
    public Guid AnnouncementId { get; set; }
    public Guid TenantUserId { get; set; }

    // The unit that put them on the list (the first one, if several).
    public Guid PropertyId { get; set; }

    // "تم الاطلاع".
    public DateTimeOffset? AcknowledgedAt { get; set; }
}

// Lookup the admin maintains. Code is stable (the app keys icons off it); names are what shows.
public class AnnouncementCategory
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public int SortOrder { get; set; }

    // Deleting deactivates: announcements already sent keep their category.
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnnouncementScope
{
    // Every unit of the owner's in one building.
    Building,
    // Chosen units.
    Units,
    // Every unit the owner has.
    AllProperties
}
