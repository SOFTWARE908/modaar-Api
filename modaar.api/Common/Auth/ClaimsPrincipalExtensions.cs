using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace modaar.api.Common.Auth;

public static class ClaimsPrincipalExtensions
{
    // MapInboundClaims is off, so "sub" survives as-is; NameIdentifier is the fallback for
    // anything that did get mapped.
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
