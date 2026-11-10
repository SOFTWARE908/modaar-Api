using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Localization;
using modaar.api.Common.Push;
using modaar.api.Common.Realtime;
using modaar.api.Common.Results;
using modaar.api.Features.Notifications.Dtos;
using modaar.api.Features.Notifications.Entities;
using modaar.api.Persistence;

namespace modaar.api.Features.Notifications.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ModaarDbContext _db;
    private readonly IRealtimeNotifier _realtime;
    private readonly IRequestLanguage _language;
    private readonly TimeProvider _time;
    private readonly PushQueue _push;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ModaarDbContext db,
        IRealtimeNotifier realtime,
        IRequestLanguage language,
        TimeProvider time,
        PushQueue push,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _realtime = realtime;
        _language = language;
        _time = time;
        _push = push;
        _logger = logger;
    }

    public async Task NotifyAsync(
        IEnumerable<Guid> recipientUserIds, NewNotification notification, CancellationToken ct)
    {
        var recipients = recipientUserIds.Distinct().ToList();
        if (recipients.Count == 0)
            return;

        try
        {
            var now = _time.GetUtcNow();

            // 1. Save first. The row is the notification; the push is only a shortcut to it.
            var rows = recipients.Select(userId => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = notification.Type,
                TitleAr = notification.TitleAr,
                TitleEn = notification.TitleEn,
                BodyAr = notification.BodyAr,
                BodyEn = notification.BodyEn,
                TargetType = notification.TargetType,
                TargetId = notification.TargetId,
                CreatedAt = now
            }).ToList();

            _db.Notifications.AddRange(rows);
            await _db.SaveChangesAsync(ct);

            // 2. Push to whoever is connected. The push has no Accept-Language — it isn't a reply
            //    to anything the recipient sent — so each recipient gets their stored language.
            //    One query for all recipients' language and unread count, not one per person.
            var recipientInfo = await _db.Users
                .AsNoTracking()
                .Where(u => recipients.Contains(u.Id))
                .Select(u => new
                {
                    u.Id,
                    u.PreferredLanguage,
                    Unread = _db.Notifications.Count(n => n.UserId == u.Id && !n.IsRead)
                })
                .ToDictionaryAsync(u => u.Id, ct);

            foreach (var row in rows)
            {
                if (!recipientInfo.TryGetValue(row.UserId, out var info))
                    continue;

                var english = info.PreferredLanguage == RequestLanguage.English;

                await _realtime.SendToUserAsync(row.UserId, HubEvents.NotificationReceived, new NotificationPushDto
                {
                    Notification = ToDto(row, english),
                    UnreadCount = info.Unread
                }, ct);
            }

            // 3. And to phones with the app closed. Queued, not sent here: the background worker
            //    talks to Firebase so this request doesn't wait on it.
            var data = new Dictionary<string, string> { ["type"] = ToCamel(notification.Type.ToString()) };
            if (notification.TargetType is { } targetType && notification.TargetId is { } targetId)
            {
                data["targetType"] = ToCamel(targetType.ToString());
                data["targetId"] = targetId.ToString();
            }

            if (!_push.TryEnqueue(new PushJob(recipients, notification.TitleAr, notification.TitleEn,
                    notification.BodyAr, notification.BodyEn, data)))
                _logger.LogWarning("Push queue full; dropped push for {Count} recipient(s).", recipients.Count);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The caller's action is already saved; losing its notification is the lesser harm.
            _logger.LogError(e, "Could not raise {Type} notification for {Count} recipient(s).",
                notification.Type, recipients.Count);
        }
    }

    public async Task<Result<PagedResultDto<NotificationDto>>> ListAsync(
        Guid userId, NotificationListQueryDto query, CancellationToken ct)
    {
        var notifications = _db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (query.Type is { } type)
            notifications = notifications.Where(n => n.Type == type);

        var totalCount = await notifications.CountAsync(ct);

        var rows = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .ToListAsync(ct);

        // Language picked after the query: IRequestLanguage can't run inside SQL.
        var english = !_language.IsArabic;

        return Result<PagedResultDto<NotificationDto>>.Ok(new PagedResultDto<NotificationDto>
        {
            Items = rows.Select(n => ToDto(n, english)).ToList(),
            TotalCount = totalCount
        });
    }

    public async Task<Result<UnreadCountDto>> GetUnreadCountAsync(Guid userId, CancellationToken ct) =>
        Result<UnreadCountDto>.Ok(new UnreadCountDto { Count = await CountUnreadAsync(userId, ct) });

    public async Task<Result<bool>> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        // Scoped to the caller: someone else's notification is "not found", not "forbidden",
        // so ids can't be probed.
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct);

        if (notification is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Notification not found.");

        // Already read: nothing to change and nothing to push.
        if (notification.IsRead)
            return Result<bool>.Ok(true);

        notification.IsRead = true;
        notification.ReadAt = _time.GetUtcNow();
        await _db.SaveChangesAsync(ct);

        await PushUnreadCountAsync(userId, ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        // One UPDATE in SQL rather than loading every unread row into memory.
        var changed = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), ct);

        if (changed > 0)
            await PushUnreadCountAsync(userId, ct);

        return Result<bool>.Ok(true);
    }

    // The bell on the user's other devices goes to the new number without waiting for a refresh.
    private async Task PushUnreadCountAsync(Guid userId, CancellationToken ct) =>
        await _realtime.SendToUserAsync(userId, HubEvents.UnreadCountChanged,
            new UnreadCountDto { Count = await CountUnreadAsync(userId, ct) }, ct);

    private Task<int> CountUnreadAsync(Guid userId, CancellationToken ct) =>
        _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    // The same camelCase the JSON API uses ("maintenanceRequest"), since data values are plain
    // strings and don't go through the JSON converter.
    private static string ToCamel(string value) => char.ToLowerInvariant(value[0]) + value[1..];

    private static NotificationDto ToDto(Notification n, bool english) => new()
    {
        Id = n.Id,
        Type = n.Type,
        Title = english ? n.TitleEn : n.TitleAr,
        Body = english ? n.BodyEn : n.BodyAr,
        TargetType = n.TargetType,
        TargetId = n.TargetId,
        IsRead = n.IsRead,
        CreatedAt = n.CreatedAt
    };
}
