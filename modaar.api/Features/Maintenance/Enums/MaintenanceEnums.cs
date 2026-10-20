using System.Text.Json.Serialization;

namespace modaar.api.Features.Maintenance.Enums;

// The client splits this in two — a list enum without Rejected and a detail enum without
// Cancelled. One stored enum covers both; the DTOs narrow it.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceRequestStatus
{
    Pending,
    Accepted,
    OnTheWay,
    InProgress,
    Completed,
    Rejected,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceTimeSlot
{
    Morning,
    Afternoon,
    Evening
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceRequestRejectedBy
{
    Owner,
    Broker,
    System
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceActionType
{
    Accept,
    Reject,
    Inquiry,
    Delegate,
    // Recorded alongside Accept when a technician is named, so the status history can date it.
    AssignTechnician,
    Quote,
    // Managed plan: the owner's answer to the quote (also recorded, with a note, when the quote
    // was under the auto-approve limit).
    ApproveQuote,
    RejectQuote,
    // A new visit date/time for the assigned technician.
    Reschedule,
    // The handler confirmed the work is done; this is what opens rating.
    Complete
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AttachmentKind
{
    Image,
    Pdf,
    Video
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NeedHelpIssueType
{
    TechnicianLate,
    ProblemNotResolved,
    BrokerNotResponding
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NeedHelpContactMethod
{
    InApp,
    WhatsApp,
    Phone
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NeedHelpStatus
{
    Open,
    Resolved,
    Closed
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenancePriority
{
    Normal = 0,
    Urgent = 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceCostBearer
{
    Owner,
    Tenant,
    // FullyManaged owners: the broker carries the cost as part of the package.
    Broker
}

// Finer than Status, for the client's card label and next-step strip. Derived on read and never
// stored, so it cannot drift from the facts it is computed from. Kept apart from Status so
// nothing that already switches on Status breaks.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceStage
{
    // Waiting for the broker to accept or reject.
    Pending,
    // Waiting for the owner to accept or reject (SelfManaged owners, where the broker is not involved).
    WithOwner,
    // Accepted; the broker has yet to price it (Managed).
    AwaitingQuote,
    // Priced; waiting for the owner to approve the quote (Managed).
    AwaitingOwnerApproval,
    // Approved and ready for someone to assign a technician.
    ReadyForTechnician,
    // A technician and a visit time are set.
    TechnicianAssigned,
    // Kept for older clients; no request reaches these today.
    Accepted,
    OnTheWay,
    InProgress,
    Completed,
    Rejected,
    Cancelled
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceHandledBy
{
    Broker,
    Owner
}

// One entry per recorded action, oldest first.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceHistoryStep
{
    Sent,
    Approved,
    Rejected,
    // The owner handed the request to the property's broker.
    Delegated,
    // The handler priced the job.
    QuoteSubmitted,
    // The owner approved the quote — or it was under their auto-approve limit (note says so).
    OwnerApproved,
    TechnicianAssigned,
    Rescheduled,
    Completed
}

// What the caller may do to the request right now — the buttons the app shows. Computed on the
// server from the owner's plan, the request's state and who is asking, so the app never has to
// re-implement those rules.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceAvailableAction
{
    Accept,
    Reject,
    Inquiry,
    Delegate,
    SubmitQuote,
    ApproveQuote,
    RejectQuote,
    AssignTechnician,
    Reschedule,
    Complete,
    Rate
}
