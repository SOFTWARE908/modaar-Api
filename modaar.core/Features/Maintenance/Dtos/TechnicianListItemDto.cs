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

    // "أقرب موعد متاح": the first free two-hour slot in the next 14 days, Riyadh time (+03:00).
    // Null when the technician has no working hours set or is booked solid.
    public DateTimeOffset? NextAvailableAt { get; init; }
}
