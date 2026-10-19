using modaar.api.Common.Dtos;
using modaar.api.Features.Notifications.Enums;

namespace modaar.api.Features.Notifications.Dtos;

// A row on the notifications page. Title and body in the reader's language.
public record NotificationDto
{
    public required Guid Id { get; init; }
    public required NotificationType Type { get; init; }

    public required string Title { get; init; }
    public required string Body { get; init; }

    public NotificationTargetType? TargetType { get; init; }
    public Guid? TargetId { get; init; }

    public required bool IsRead { get; init; }

    // The app groups by this into today / yesterday / earlier.
    public required DateTimeOffset CreatedAt { get; init; }
}

public record NotificationListQueryDto : PagedRequestDto
{
    // Omit for "all".
    public NotificationType? Type { get; init; }
}

public record UnreadCountDto
{
    public required int Count { get; init; }
}

// What the hub sends with notificationReceived: the new row plus the bell's new number, so the
// app updates both without a second request.
public record NotificationPushDto
{
    public required NotificationDto Notification { get; init; }
    public required int UnreadCount { get; init; }
}
