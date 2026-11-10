using modaar.api.Features.Notifications.Enums;

namespace modaar.api.Features.Notifications.Entities;

// One row per recipient: the same event sent to an owner and their broker is two rows, because
// each reads (and marks read) their own.
//
// The text is written in both languages when the event happens, like every other _Ar/_En pair in
// the project, so the list can be served in whatever language the reader asks for later.
public class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }

    public string TitleAr { get; set; } = null!;
    public string TitleEn { get; set; } = null!;
    public string BodyAr { get; set; } = null!;
    public string BodyEn { get; set; } = null!;

    // Where a tap takes the user. Both null for a notification that opens nothing.
    public NotificationTargetType? TargetType { get; set; }
    public Guid? TargetId { get; set; }

    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
