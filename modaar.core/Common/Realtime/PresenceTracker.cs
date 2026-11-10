using Microsoft.EntityFrameworkCore;
using modaar.api.Persistence;

namespace modaar.api.Common.Realtime;

// Who has the app open right now, from their hub connections ("متصل الآن").
//
// A user is online while they hold at least one connection (phone and tablet count together).
// When the last one closes they stay "online" for a short grace period, so a dropped network or a
// quick app switch doesn't flicker them offline and back. If nothing reconnects in that time, they
// go offline: LastSeenAt is saved, and the people they chat with are told.
//
// In memory, so it holds for a single server. More than one server would need the counts in a
// shared store (Redis) — the same point at which SignalR itself needs a backplane.
public sealed class PresenceTracker
{
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Dictionary<Guid, int> _connections = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _goingOffline = new();

    private readonly IServiceScopeFactory _scopes;
    private readonly IRealtimeNotifier _realtime;
    private readonly TimeProvider _time;
    private readonly ILogger<PresenceTracker> _logger;

    public PresenceTracker(IServiceScopeFactory scopes, IRealtimeNotifier realtime, TimeProvider time, ILogger<PresenceTracker> logger)
    {
        _scopes = scopes;
        _realtime = realtime;
        _time = time;
        _logger = logger;
    }

    public bool IsOnline(Guid userId)
    {
        lock (_gate)
            return _connections.ContainsKey(userId) || _goingOffline.ContainsKey(userId);
    }

    public async Task ConnectedAsync(Guid userId)
    {
        bool cameOnline;
        lock (_gate)
        {
            // Back within the grace period: they never went offline as far as anyone saw.
            var wasGoingOffline = _goingOffline.Remove(userId, out var pending);
            pending?.Cancel();

            var count = _connections.GetValueOrDefault(userId);
            _connections[userId] = count + 1;
            cameOnline = count == 0 && !wasGoingOffline;
        }

        if (cameOnline)
            await BroadcastAsync(userId, isOnline: true, lastSeenAt: null);
    }

    public void Disconnected(Guid userId)
    {
        CancellationTokenSource? wait = null;
        lock (_gate)
        {
            var count = _connections.GetValueOrDefault(userId) - 1;
            if (count > 0)
            {
                _connections[userId] = count;
                return;
            }

            _connections.Remove(userId);
            wait = new CancellationTokenSource();
            _goingOffline[userId] = wait;
        }

        _ = GoOfflineAfterGraceAsync(userId, wait);
    }

    private async Task GoOfflineAfterGraceAsync(Guid userId, CancellationTokenSource wait)
    {
        try
        {
            await Task.Delay(Grace, _time, wait.Token);
        }
        catch (OperationCanceledException)
        {
            return; // reconnected in time
        }

        lock (_gate)
        {
            // Only if it is still this same pending offline (not replaced by a reconnect + drop).
            if (!_goingOffline.TryGetValue(userId, out var current) || current != wait)
                return;
            _goingOffline.Remove(userId);
        }

        var now = _time.GetUtcNow();
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ModaarDbContext>();
            await db.Users.Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastSeenAt, now));
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not save last-seen for {UserId}.", userId);
        }

        await BroadcastAsync(userId, isOnline: false, lastSeenAt: now);
    }

    // Only to the people this user has a conversation with — presence isn't anyone else's business.
    private async Task BroadcastAsync(Guid userId, bool isOnline, DateTimeOffset? lastSeenAt)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ModaarDbContext>();

            var contacts = await db.ConversationParticipants.AsNoTracking()
                .Where(p => p.UserId != userId
                         && db.ConversationParticipants.Any(me => me.ConversationId == p.ConversationId && me.UserId == userId))
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync();

            if (contacts.Count > 0)
                await _realtime.SendToUsersAsync(contacts, HubEvents.PresenceChanged, new { userId, isOnline, lastSeenAt });
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not broadcast presence for {UserId}.", userId);
        }
    }
}
