namespace modaar.api.Features.Maintenance.Dtos
{
    public record MaintenanceServiceTypeDto
    {
        public required Guid Id { get; init; }

        // The stable slug to key icons and special cases off. Never changes for a given category.
        public required string Code { get; init; }

        public required string Name { get; init; }

        // Null until an icon has been uploaded for this category; fall back to a generic tile.
        public string? IconUrl { get; init; }

        public required int SortOrder { get; init; }
    }
}
