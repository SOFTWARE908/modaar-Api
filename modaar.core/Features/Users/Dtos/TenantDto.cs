namespace modaar.api.Features.Users.Dtos;

// A tenant as seen by someone else — the owner or broker looking at a property or a maintenance
// request. Projected from User where AccountType is Tenant; there is no Tenants table.
public record TenantDto
{
    public required Guid Id { get; init; }

    // Null until the tenant completes their profile, so the app has to cope with a nameless
    // tenant on a contract that was set up for them.
    public string? Name { get; init; }

    public string? AvatarUrl { get; init; }

    // Full international number, matching the identifier used at login.
    public required string PhoneNumber { get; init; }
}
