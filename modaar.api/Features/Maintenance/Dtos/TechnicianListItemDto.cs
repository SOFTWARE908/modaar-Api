namespace modaar.api.Features.Maintenance.Dtos;

// One row of "فنيو الشبكة" on the assign-technician screen.
public record TechnicianListItemDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? AvatarUrl { get; init; }

    // Localized job title, e.g. "سبّاك".
    public required string Role { get; init; }

    // The categories they can be assigned to, localized.
    public required IReadOnlyList<string> Specialties { get; init; }

    public required decimal AverageRating { get; init; }
    public required int RatingsCount { get; init; }
    public required int CompletedJobsCount { get; init; }

    // "أقرب موعد متاح". Always null for now: technicians have no calendar yet.
    public DateTimeOffset? NextAvailableAt { get; init; }
}
