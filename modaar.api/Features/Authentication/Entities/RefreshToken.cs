using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Authentication.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // One sign-in on one phone. Rotation and role switches keep it; logout ends only this one,
    // so the account's other phones stay signed in. Carried in the access token as "sid".
    public Guid SessionId { get; set; }

    public AccountType? ActiveRole { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // Set when this token has been used and rotated; points at the new refresh token that replaced it.
    public Guid? ReplacedByTokenId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
