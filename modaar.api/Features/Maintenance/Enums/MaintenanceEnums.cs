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
    Quote
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
    Tenant
}

// Finer than Status, for the client's card label and next-step strip. Derived on read and never
// stored, so it cannot drift from the facts it is computed from. Kept apart from Status so
// nothing that already switches on Status breaks.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceStage
{
    Pending,
    WithOwner,
    AwaitingQuote,
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

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceHistoryStep
{
    Sent,
    Approved,
    Rejected,
    // The owner handed the request to the property's broker.
    Delegated,
    TechnicianAssigned,
    Completed
}
