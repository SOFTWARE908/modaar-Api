namespace modaar.api.Common.Realtime;

// What a service calls to push something to a user's open app. Services depend on this, not on
// SignalR, so adding FCM or a Redis backplane later changes RealtimeNotifier and nothing else.
//
// Fire-and-forget by nature: if the user has no open connection the call simply reaches nobody.
// That is fine, because whatever is pushed has already been saved and the app loads it over REST
// when it next opens.
public interface IRealtimeNotifier
{
    Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default);

    Task SendToUsersAsync(IEnumerable<Guid> userIds, string eventName, object payload, CancellationToken ct = default);
}
