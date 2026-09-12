using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance.Entities;

// Every accept / reject / inquiry / delegate, kept as history rather than overwriting the
// request. Status tells you where a request is; this tells you how it got there and who decided.
public class MaintenanceRequestAction
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }
    public Guid ActorUserId { get; set; }

    public MaintenanceActionType Type { get; set; }

    // Rejection reason, inquiry message, or delegation note depending on Type.
    public string? Message { get; set; }

    // Set on Delegate: the brokerage the owner handed the request to.
    public Guid? DelegatedToBrokerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

// The tenant's rating of the technician after completion. One per request.
public class MaintenanceRating
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    // Snapshot: who was actually rated. The request's assignment could change afterwards.
    public Guid? TechnicianId { get; set; }

    public int Stars { get; set; }
    public string? Comment { get; set; }

    // Fixed set of translation keys from the client, stored as JSON: never queried, always read
    // with the rating, and replaced as a whole set.
    public List<string> TagKeys { get; set; } = [];

    // The UI offers 10/20/50/100 SAR or a custom amount; both land in one column.
    public decimal? TipAmount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// The tenant's escalation on a request that is going wrong.
public class MaintenanceNeedHelp
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }
    public Guid RaisedByUserId { get; set; }

    public NeedHelpIssueType IssueType { get; set; }
    public string? Description { get; set; }
    public NeedHelpContactMethod ContactMethod { get; set; }

    public NeedHelpStatus Status { get; set; } = NeedHelpStatus.Open;
    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// Extension row on a User with AccountType.Technician — same shape as Broker. Name and photo
// come from User; this holds only what is true of a tradesperson.
public class Technician
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    // The brokerage this technician works for. Null for an independent or platform-wide one.
    public Guid? BrokerId { get; set; }

    // Localized job title shown under the name — "Painter", "سبّاك".
    public string RoleAr { get; set; } = null!;
    public string RoleEn { get; set; } = null!;

    // Which categories they can be assigned to. Display and filtering are both small-scale here,
    // so JSON is enough until assignment is automated.
    public List<MaintenanceServiceType> Skills { get; set; } = [];

    // Rollup over ratings, recalculated on write like the broker's.
    public decimal RatingAverage { get; set; }
    public int RatingsCount { get; set; }

    public bool IsAvailable { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
