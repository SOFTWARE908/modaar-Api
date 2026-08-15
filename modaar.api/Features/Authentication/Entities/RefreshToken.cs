namespace modaar.api.Features.Authentication.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // Set when this token has been used and rotated; points at the new refresh token that replaced it.
    public Guid? ReplacedByTokenId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
