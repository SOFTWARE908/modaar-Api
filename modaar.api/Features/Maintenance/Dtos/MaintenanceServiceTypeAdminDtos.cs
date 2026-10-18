namespace modaar.api.Features.Maintenance.Dtos
{
    public record MaintenanceServiceTypeAdminDto
    {
        public required Guid Id { get; init; }
        public required string Code { get; init; }
        public required string NameAr { get; init; }
        public required string NameEn { get; init; }
        public string? IconUrl { get; init; }
        public required int SortOrder { get; init; }
        public required bool IsActive { get; init; }

        // How many requests have been filed under it. The admin screen uses this to say why a
        // category cannot be deleted, only deactivated.
        public required int RequestsCount { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }
    }

    public record CreateMaintenanceServiceTypeDto
    {
        public required string Code { get; init; }
        public required string NameAr { get; init; }
        public required string NameEn { get; init; }
        public string? IconUrl { get; init; }
        public int? SortOrder { get; init; }
    }

    // Every field optional: absent means leave it alone. Code is deliberately absent — it is the
    // client's key and changing it would silently break whatever maps an icon to it.
    public record UpdateMaintenanceServiceTypeDto
    {
        public string? NameAr { get; init; }
        public string? NameEn { get; init; }

        // Send "" to clear the icon, omit to leave it.
        public string? IconUrl { get; init; }

        public int? SortOrder { get; init; }
        public bool? IsActive { get; init; }
    }
}
