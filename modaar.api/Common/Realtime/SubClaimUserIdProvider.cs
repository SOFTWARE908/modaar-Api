using Microsoft.AspNetCore.SignalR;
using modaar.api.Common.Auth;

namespace modaar.api.Common.Realtime;

// Tells SignalR which user a connection belongs to. SignalR's default looks for
// ClaimTypes.NameIdentifier, but this API keeps raw JWT claim names (MapInboundClaims = false),
// so the user id is in "sub" and the default would find nothing.
//
// With this in place Clients.User("<userId>") reaches every connection of that user — phone and
// tablet alike — without us tracking connection ids or groups by hand.
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User.GetUserId()?.ToString();
}
