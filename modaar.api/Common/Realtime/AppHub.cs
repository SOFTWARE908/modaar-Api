using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace modaar.api.Common.Realtime;

// The one hub the app connects to: wss://<host>/hubs/app?access_token=<jwt>.
//
// Deliberately thin. The server pushes through IRealtimeNotifier from ordinary services; nothing
// is sent or saved through hub methods, because a hub method has no validation pipeline, no
// response envelope and no HTTP status — sending a chat message stays a REST call. The hub's job
// is to hold the connection open and know whose it is.
//
// [Authorize] rejects the connection without a valid token. No active-role check: a user's
// notifications and messages belong to the person, whichever role they are using right now.
[Authorize]
public sealed class AppHub : Hub
{
    private readonly ILogger<AppHub> _logger;
    private readonly PresenceTracker _presence;

    public AppHub(ILogger<AppHub> logger, PresenceTracker presence)
    {
        _logger = logger;
        _presence = presence;
    }

    // Runs once per connection. Context.UserIdentifier comes from SubClaimUserIdProvider.
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Realtime: user {UserId} connected ({ConnectionId}).",
            Context.UserIdentifier, Context.ConnectionId);

        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            await _presence.ConnectedAsync(userId);

        await base.OnConnectedAsync();
    }

    // Runs when the app closes the connection or it drops. SignalR removes the connection from the
    // user on its own; presence counts it down (and goes offline after a grace period).
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Realtime: user {UserId} disconnected ({ConnectionId}).",
            Context.UserIdentifier, Context.ConnectionId);

        if (Guid.TryParse(Context.UserIdentifier, out var userId))
            _presence.Disconnected(userId);

        return base.OnDisconnectedAsync(exception);
    }

    // Test method: the app calls connection.invoke("Ping", "hello") and every open connection of
    // the same user receives "pong". Proves auth, the user mapping and server→client delivery in
    // one round trip.
    public Task Ping(string message) =>
        Clients.User(Context.UserIdentifier!).SendAsync(HubEvents.Pong, new
        {
            message,
            connectionId = Context.ConnectionId,
            serverTime = DateTimeOffset.UtcNow
        });
}
