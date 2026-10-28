namespace modaar.api.Features.Buildings.Entities;

// A heading on the building rules screen ("أوقات الهدوء", "المواقف", …) and the rules under it.
// Sections and rules are only added (at the end) or deleted, never edited in place.
public class BuildingRuleSection
{
    public Guid Id { get; set; }
    public Guid BuildingId { get; set; }

    public string Title { get; set; } = null!;
    public int SortOrder { get; set; }

    public List<BuildingRule> Rules { get; set; } = [];
}

public class BuildingRule
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }

    public string Text { get; set; } = null!;
    public int SortOrder { get; set; }
}
