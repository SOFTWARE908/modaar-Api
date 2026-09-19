using modaar.api.Common.Dtos;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Users.Dtos;

namespace modaar.api.Features.Maintenance.Dtos;

public record TechnicianDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    // Localized job title, picked by Accept-Language.
    public required string Role { get; init; }

    public string? ImageUrl { get; init; }
    public required decimal AverageRating { get; init; }
}

public record MaintenanceRequestListItemDto
{
    public required Guid Id { get; init; }
    public required string RequestCode { get; init; }

    public required string Title { get; init; }
    public required MaintenanceServiceType ServiceType { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    // Pre-composed line, e.g. "Nov 4, 2025 · Morning". Null while nothing is scheduled.
    public string? VisitSummary { get; init; }

    public required MaintenanceRequestStatus Status { get; init; }
    public TechnicianDto? AssignedTechnician { get; init; }
}

public record MaintenanceRequestDetailsDto
{
    public required Guid Id { get; init; }
    public required string RequestNumber { get; init; }

    public required MaintenanceRequestStatus Status { get; init; }
    public MaintenanceRequestRejectedBy? RejectedBy { get; init; }
    public string? RejectionReason { get; init; }

    // The scheduled visit if one was agreed, otherwise what the tenant asked for.
    public required DateOnly VisitDate { get; init; }
    public required bool VisitDayIsTomorrow { get; init; }
    public required string VisitTimeRange { get; init; }
    public required MaintenanceTimeSlot VisitTimePeriod { get; init; }

    public TechnicianDto? AssignedTechnician { get; init; }

    // Present once the tenant has rated the visit.
    public int? TenantRatingStars { get; init; }

    public required string ProblemDescription { get; init; }
    public required MaintenanceServiceType ServiceType { get; init; }
    public required IReadOnlyList<string> AttachmentImageUrls { get; init; }

    public required Guid PropertyId { get; init; }
    public required string PropertyTitle { get; init; }

    // Owner and broker views only; null when the caller is the tenant looking at their own request.
    public TenantDto? Tenant { get; init; }
}

public record CreateMaintenanceRequestDto
{
    public required MaintenanceServiceType ServiceType { get; init; }
    public required string Description { get; init; }

    public required DateOnly PreferredVisitDate { get; init; }
    public required MaintenanceTimeSlot VisitTimeSlot { get; init; }

    // Uploaded beforehand via POST /maintenance-requests/attachments.
    public IReadOnlyList<Guid>? AttachmentIds { get; init; }

    // Optional: defaults to the tenant's single active lease. Required only when they hold more
    // than one, which the schema permits.
    public Guid? PropertyId { get; init; }
}

public record CreateMaintenanceRequestResponseDto
{
    public required Guid Id { get; init; }
    public required string RequestCode { get; init; }
}

public record AttachmentDto
{
    public required Guid Id { get; init; }
    public required string Url { get; init; }
    public required string FileName { get; init; }
    public required AttachmentKind Kind { get; init; }
}

public record MaintenanceListQueryDto : PagedRequestDto
{
    // Comma-separated in the query string; model-bound as a list.
    public IReadOnlyList<MaintenanceRequestStatus>? Status { get; init; }

    public MaintenanceServiceType? ServiceType { get; init; }
    public Guid? PropertyId { get; init; }

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}
