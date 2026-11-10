using modaar.api.Common.Realtime;

namespace modaar.admin.Auth;

// Core's NotificationService needs an IRealtimeNotifier. The live channel (SignalR) belongs to the
// API; from the panel there is nobody to push to, so this does nothing. The notification rows are
// still saved and the app shows them in its list.
public sealed class NoRealtimeNotifier : IRealtimeNotifier
{
    public Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task SendToUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken ct = default) =>
        Task.CompletedTask;
}
