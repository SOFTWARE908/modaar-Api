namespace modaar.api.Features.Buildings.Entities;

// A heading on the building rules screen ("أوقات الهدوء", "المواقف", …). One list for every
// building, managed by the admin; owners only write rules under them.
public class BuildingRuleCategory
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;

    // Link to the icon image the app shows beside the name.
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }

    // Deleting deactivates: rules already written under it stay visible, but no new ones go in.
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// One rule of one building, under a category. Only added (at the end of its category) or deleted.
public class BuildingRule
{
    public Guid Id { get; set; }
    public Guid BuildingId { get; set; }
    public Guid CategoryId { get; set; }

    public string Text { get; set; } = null!;
    public int SortOrder { get; set; }
}

// "أرقام مهمة" under the rules: the guard, emergency, civil defence… typed in by the owner.
public class BuildingContact
{
    public Guid Id { get; set; }
    public Guid BuildingId { get; set; }

    public string Name { get; set; } = null!;

    // Line under the name, e.g. "متاح 24 ساعة".
    public string? Note { get; set; }
    public string PhoneNumber { get; set; } = null!;

    // Shown in red (emergency, civil defence) instead of the building's own green.
    public bool IsEmergency { get; set; }
    public int SortOrder { get; set; }
}
