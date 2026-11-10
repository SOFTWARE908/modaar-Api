using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Notifications.Dtos;
using modaar.api.Features.Notifications.Enums;

namespace modaar.api.Features.Notifications.Services;

// What a feature hands over to raise a notification: the text in both languages and where a tap
// should go. Who receives it is a separate argument, because the same event often goes to several
// people.
public sealed record NewNotification(
    NotificationType Type,
    string TitleAr, string TitleEn,
    string BodyAr, string BodyEn,
    NotificationTargetType? TargetType = null,
    Guid? TargetId = null);

public interface INotificationService
{
    // Saves one row per recipient, then pushes it to whoever has the app open. Never throws: a
    // failed notification must not undo the action that caused it. Call it after that action
    // has been saved.
    Task NotifyAsync(IEnumerable<Guid> recipientUserIds, NewNotification notification, CancellationToken ct);

    Task<Result<PagedResultDto<NotificationDto>>> ListAsync(
        Guid userId, NotificationListQueryDto query, CancellationToken ct);

    Task<Result<UnreadCountDto>> GetUnreadCountAsync(Guid userId, CancellationToken ct);

    Task<Result<bool>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct);

    Task<Result<bool>> MarkAllReadAsync(Guid userId, CancellationToken ct);
}
