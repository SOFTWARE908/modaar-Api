using Microsoft.IdentityModel.JsonWebTokens;
using modaar.api.Features.Authentication.Enums;
using System.Security.Claims;

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

    public static AccountType? GetActiveRole(this ClaimsPrincipal principal) =>
        Enum.TryParse<AccountType>(principal.FindFirst("active_role")?.Value, out var role)
            ? role
            : null;

    public static IReadOnlyList<AccountType> GetAccountTypes(this ClaimsPrincipal principal) =>
        principal.FindAll("account_type")
            .Select(c => Enum.TryParse<AccountType>(c.Value, out var t) ? (AccountType?)t : null)
            .Where(t => t is not null)
            .Select(t => t!.Value)
            .ToList();
}
