using Microsoft.AspNetCore.SignalR;

namespace modaar.api.Common.Realtime;

// IHubContext<AppHub> is SignalR's handle for sending to the hub from outside it — from a service,
// a controller or a background job — where there is no hub instance and no "current connection".
public sealed class RealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<AppHub> _hub;
    private readonly ILogger<RealtimeNotifier> _logger;

    public RealtimeNotifier(IHubContext<AppHub> hub, ILogger<RealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default) =>
        SendAsync(() => _hub.Clients.User(userId.ToString()).SendAsync(eventName, payload, ct), eventName);

    public Task SendToUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken ct = default) =>
        SendAsync(() => _hub.Clients.Users(userIds.Select(id => id.ToString()).ToList()).SendAsync(eventName, payload, ct), eventName);

    // A push failing must never fail the request that caused it: the data is already saved and
    // the app will load it over REST anyway. Log and move on.
    private async Task SendAsync(Func<Task> send, string eventName)
    {
        try
        {
            await send();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning(e, "Realtime push of {Event} failed.", eventName);
        }
    }
}
