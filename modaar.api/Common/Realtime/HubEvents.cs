namespace modaar.api.Common.Realtime;

public static class RealtimeRoutes
{
    // Where the hub is mapped. Also used by the JWT setup to accept ?access_token= on this path only.
    public const string AppHub = "/hubs/app";
}

// The names of the events the server pushes over the hub. The mobile app listens for these exact
// strings (connection.on("notificationReceived", ...)), so they are a contract like a route: keep
// them here, never as literals scattered through services, and never rename one silently.
public static class HubEvents
{
    // A new notification for the bell. Payload: the notification DTO and the new unread count.
    public const string NotificationReceived = "notificationReceived";

    // A new chat message in one of the user's conversations.
    public const string MessageReceived = "messageReceived";

    // The unread badge changed without a new item arriving, e.g. read on another device.
    public const string UnreadCountChanged = "unreadCountChanged";

    // Someone you have a conversation with came online or went offline.
    // Payload: { userId, isOnline, lastSeenAt }.
    public const string PresenceChanged = "presenceChanged";

    // Reply to the hub's Ping method. For testing a connection end to end.
    public const string Pong = "pong";
}
