namespace modaar.api.Features.Maintenance.Entities
{
    public class MaintenanceServiceType
    {
        public Guid Id { get; set; }

        // A stable, unchanging slug — "cleaning", "paintRepair". This is what the client keys
        // behaviour off: the id differs between databases and the names are translated, so neither
        // can carry a hardcoded icon or a special case. Never edit a code once anything ships
        // against it; add a new row instead.
        public string Code { get; set; } = null!;

        public string NameAr { get; set; } = null!;
        public string NameEn { get; set; } = null!;

        // Absolute URL of the tile icon. Nullable so a category can be created before its asset is
        // uploaded; the client should fall back to a generic icon rather than an empty tile.
        public string? IconUrl { get; set; }

        // Order in the picker. Low first. Gaps are fine and make inserting between two rows easy.
        public int SortOrder { get; set; }

        // False takes it out of every picker while leaving existing requests resolvable. There is no
        // delete: a request raised against a category keeps pointing at it forever.
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
