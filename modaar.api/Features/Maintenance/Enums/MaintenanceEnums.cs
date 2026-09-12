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
    Delegate
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
