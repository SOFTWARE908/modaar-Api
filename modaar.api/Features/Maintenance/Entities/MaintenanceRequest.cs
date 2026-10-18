using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance.Entities;

// A tenant's request for work on their unit, and the workflow the owner or broker runs on it.
public class MaintenanceRequest
{
    public Guid Id { get; set; }

    // Display code shown in the app, e.g. "D-654321". Unique, generated at creation.
    public string RequestNumber { get; set; } = null!;

    public Guid PropertyId { get; set; }
    public Guid TenantUserId { get; set; }

    // The lease the request was raised under. Snapshotted so the request stays attached to the
    // right tenancy after the tenant moves out and a new lease starts on the unit.
    public Guid? ContractId { get; set; }

    public Guid ServiceTypeId { get; set; }

    // Short headline the tenant gives the problem, e.g. "تسرّب مياه أسفل مغسلة المطبخ". Null on
    // requests raised before it existed; readers fall back to the category name.
    public string? Title { get; set; }

    public string ProblemDescription { get; set; } = null!;

    // Where in the unit the problem is — "المطبخ". Free text; the design has no fixed list.
    public string? Location { get; set; }

    // Set by the tenant when raising the request; the owner may change it while triaging.
    // Normal unless someone says otherwise, which is also what every existing row becomes.
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Normal;

    public MaintenanceRequestStatus Status { get; set; } = MaintenanceRequestStatus.Pending;

    // What the tenant asked for at creation. Optional: the new-request sheet has no date picker,
    // so the owner or broker proposes the visit when accepting. Set together or not at all.
    public DateOnly? PreferredVisitDate { get; set; }
    public MaintenanceTimeSlot? PreferredTimeSlot { get; set; }

    // What was actually agreed once someone accepted. Null while pending, and may differ from
    // what the tenant asked for.
    public DateOnly? ScheduledVisitDate { get; set; }
    public MaintenanceTimeSlot? ScheduledTimeSlot { get; set; }

    public Guid? AssignedTechnicianId { get; set; }

    // Set on accept when the job needs pricing before work starts. The request then sits at the
    // awaitingQuote stage until a quote is submitted.
    public bool QuoteRequired { get; set; }

    // The price for the work, once someone has submitted one. All set together.
    public decimal? QuotePartsCost { get; set; }
    public decimal? QuoteLaborCost { get; set; }
    public MaintenanceCostBearer? QuoteCostBearer { get; set; }
    public string? QuoteDescription { get; set; }
    public DateTimeOffset? QuotedAt { get; set; }

    // Set together when the request is rejected; the client shows who turned it down.
    public MaintenanceRequestRejectedBy? RejectedBy { get; set; }
    public string? RejectionReason { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// A real table, not a JSON column: files are uploaded one at a time and get an id back before
// the request exists, so RequestId is null until create links them.
public class MaintenanceAttachment
{
    public Guid Id { get; set; }

    public Guid? RequestId { get; set; }

    // Who uploaded it, so an orphaned upload can be cleaned up and can't be linked by anyone else.
    public Guid UploadedByUserId { get; set; }

    public string Url { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public AttachmentKind Kind { get; set; }
    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
