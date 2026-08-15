using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Users.Entities;

public class User
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public string NationalId { get; set; } = null!;

    public AccountType AccountType { get; set; }

    // Nullable: OTP-only users may never set a password.
    public string? PasswordHash { get; set; }
    public string? ProfileImageUrl { get; set; }

    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
