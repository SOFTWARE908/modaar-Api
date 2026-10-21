using modaar.api.Common.Dtos;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Maintenance.Dtos;

public record TechnicianDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }

    // Localized job title, picked by Accept-Language.
    public required string Role { get; init; }

    public string? ImageUrl { get; init; }
    public required decimal AverageRating { get; init; }

    // International format, "+9665…", for the call button on the technician card.
    public string? PhoneNumber { get; init; }

    // The categories they handle, localized and joined: "سباكة، كهرباء". Null if none set.
    public string? Specialty { get; init; }
}

public record MaintenanceRequestListItemDto
{
    public required Guid Id { get; init; }
    public required string RequestCode { get; init; }
    public MaintenancePriority Priority { get; init; }

    // The tenant's own headline, or the category name on requests raised without one.
    public required string Title { get; init; }
    public required MaintenanceServiceTypeDto ServiceType { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    // Pre-composed line, e.g. "Nov 4, 2025 · Morning". Null while nothing is scheduled.
    // In Arabic it comes out in the Hijri calendar; format VisitDate in the app instead.
    public string? VisitSummary { get; init; }

    // The visit as data: scheduled if agreed, otherwise what the tenant asked for. VisitTime is
    // the exact time once a technician is assigned ("14:30:00"), otherwise null.
    public DateOnly? VisitDate { get; init; }
    public TimeOnly? VisitTime { get; init; }
    public MaintenanceTimeSlot? VisitTimePeriod { get; init; }

    public required MaintenanceRequestStatus Status { get; init; }

    // Finer-grained than Status (adds withOwner and awaitingQuote) for the card label.
    public required MaintenanceStage Stage { get; init; }

    // Text for the strip under the card, localized by Accept-Language. Null when there is no
    // next step to show.
    public string? NextActionLabel { get; init; }

    public TechnicianDto? AssignedTechnician { get; init; }

    public required Guid PropertyId { get; init; }

    // The unit the request is against — the navy strip across the top of the card.
    public required string PropertyTitle { get; init; }

    // Who owns it. Null until that owner completes their profile, which is normal on an
    // account created from a phone number and never filled in.
    public string? OwnerName { get; init; }

    // Who raised it, for the owner's and broker's cards. Null when the caller is that tenant.
    public string? TenantName { get; init; }

    // The quote total once priced ("اكتمل · 2,400 ر.س"), null before.
    public decimal? Cost { get; init; }

    // Once the tenant rated it ("تقييم 5/5"): stars, and the average of the four criteria.
    public int? RatingStars { get; init; }
    public decimal? RatingAverage { get; init; }

    // The caller's buttons on this card — the same list the details screen returns.
    public required IReadOnlyList<MaintenanceAvailableAction> AvailableActions { get; init; }
}

public record MaintenanceRequestDetailsDto
{
    public required Guid Id { get; init; }
    public required string RequestNumber { get; init; }
    public MaintenancePriority Priority { get; init; }

    public required string Title { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }

    // Where in the unit, e.g. "المطبخ". Null when the tenant didn't say.
    public string? Location { get; init; }

    public required MaintenanceRequestStatus Status { get; init; }
    public required MaintenanceStage Stage { get; init; }
    public string? NextActionLabel { get; init; }

    public MaintenanceRequestRejectedBy? RejectedBy { get; init; }
    public string? RejectionReason { get; init; }

    // Who is dealing with the request: the managing broker, or the owner directly.
    public required MaintenanceHandledBy HandledBy { get; init; }
    public string? HandledByName { get; init; }

    // The owner's subscription when the request was raised — it decides the workflow below.
    public required OwnerSubscriptionPlan SubscriptionPlan { get; init; }

    // What the caller may do right now. Show exactly these buttons; the server refuses the rest.
    public required IReadOnlyList<MaintenanceAvailableAction> AvailableActions { get; init; }

    // Oldest first. Only steps that actually happened; the client draws the rest as upcoming.
    public required IReadOnlyList<MaintenanceStatusHistoryItemDto> StatusHistory { get; init; }

    // The scheduled visit if one was agreed, otherwise what the tenant asked for. All null while
    // no one has proposed a visit yet.
    public DateOnly? VisitDate { get; init; }
    public required bool VisitDayIsTomorrow { get; init; }
    public string? VisitTimeRange { get; init; }
    public MaintenanceTimeSlot? VisitTimePeriod { get; init; }

    // The exact visit time, once a technician is assigned with one ("14:30"). Null before that.
    public TimeOnly? VisitTime { get; init; }

    public TechnicianDto? AssignedTechnician { get; init; }

    // Present once someone has priced the work.
    public MaintenanceQuoteDto? Quote { get; init; }

    // Present once the tenant has rated the visit. Kept alongside Rating for older clients.
    public int? TenantRatingStars { get; init; }
    public MaintenanceRatingDto? Rating { get; init; }

    public required string ProblemDescription { get; init; }
    public required MaintenanceServiceTypeDto ServiceType { get; init; }
    public required IReadOnlyList<string> AttachmentImageUrls { get; init; }

    public required Guid PropertyId { get; init; }
    public required string PropertyTitle { get; init; }

    // Owner and broker views only; null when the caller is the tenant looking at their own request.
    public TenantDto? Tenant { get; init; }

    // The broker's view of whose property it is (call, avatar). Null for everyone else.
    public MaintenanceOwnerDto? Owner { get; init; }

    // Managed plan, owner and broker only: quotes at or under this are approved without asking
    // the owner ("يعتمد العروض فوق 300 ر.س"). Null when there is no limit or it doesn't apply.
    public decimal? AutoApproveLimit { get; init; }
}

public record MaintenanceOwnerDto
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public string? PhoneNumber { get; init; }
    public string? AvatarUrl { get; init; }
}

public record MaintenanceStatusHistoryItemDto
{
    public required MaintenanceHistoryStep Step { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? ActorName { get; init; }
    public string? Note { get; init; }
}

public record MaintenanceQuoteDto
{
    public required decimal PartsCost { get; init; }
    public required decimal LaborCost { get; init; }
    public required decimal Total { get; init; }
    public required MaintenanceCostBearer CostBearer { get; init; }
    public string? Description { get; init; }
    public DateTimeOffset? QuotedAt { get; init; }

    // Managed plan. Null while the owner has not answered. AutoApproved = under the owner's limit.
    public DateTimeOffset? ApprovedAt { get; init; }
    public bool AutoApproved { get; init; }
}

public record MaintenanceRatingDto
{
    // Null on ratings submitted before the four-part form existed; Average then equals Stars.
    public int? TechnicianRating { get; init; }
    public int? RepairQualityRating { get; init; }
    public int? ResponseSpeedRating { get; init; }
    public int? BrokerCommunicationRating { get; init; }

    public required decimal Average { get; init; }
    public required int Stars { get; init; }

    public required IReadOnlyList<string> TagKeys { get; init; }
    public string? Comment { get; init; }
    public required DateTimeOffset RatedAt { get; init; }
}

public record CreateMaintenanceRequestDto
{
    public required Guid ServiceTypeId { get; init; }

    // Optional headline; the list falls back to the category name without one.
    public string? Title { get; init; }
    public required string Description { get; init; }
    public string? Location { get; init; }
    public MaintenancePriority? Priority { get; init; }

    // Optional, but both or neither. Without them the owner or broker sets the visit on accept.
    public DateOnly? PreferredVisitDate { get; init; }
    public MaintenanceTimeSlot? VisitTimeSlot { get; init; }

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

    public Guid? ServiceTypeId { get; init; }
    public Guid? PropertyId { get; init; }

    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
}

// GET /maintenance-requests: the page, plus how many different owners the whole filtered list
// spans — "6 طلبات لدى 3 ملاك" — counted over every match, not just this page.
public record MaintenanceListResultDto : PagedResultDto<MaintenanceRequestListItemDto>
{
    public required int OwnersCount { get; init; }
}
