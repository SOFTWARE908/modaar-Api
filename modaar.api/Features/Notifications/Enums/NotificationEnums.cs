using System.Text.Json.Serialization;

namespace modaar.api.Features.Notifications.Enums;

// The filter chips on the notifications page and the icon on each row.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    Maintenance,
    Payments,
    Messages,
    Contracts
}

// What tapping the notification opens. The app pairs it with TargetId to navigate.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationTargetType
{
    MaintenanceRequest,
    Payment,
    Conversation,
    Contract
}
