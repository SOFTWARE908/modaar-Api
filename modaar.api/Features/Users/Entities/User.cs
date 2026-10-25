using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Users.Entities;

public class User
{
    public Guid Id { get; set; }

    // The mobile number is the account's identity: it is the only field set when the account is
    // created, and the only one guaranteed to be present. Everything below is filled in later.
    public string PhoneNumber { get; set; } = null!;
    public string CountryCode { get; set; } = null!;

    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? NationalId { get; set; }

    // Identity details imported from Nafath, never typed by the user. Null until that import
    // exists.
    public DateOnly? DateOfBirth { get; set; }
    public string? Nationality { get; set; }

    // Defaulted on creation; the user can change it while completing their profile.
    public List<AccountType> AccountTypes { get; set; } = [];

    public AccountType PrimaryAccountType =>
        AccountTypes.Count > 0 ? AccountTypes[0] : AccountType.Tenant;

    public ProfileStatus ProfileStatus { get; set; } = ProfileStatus.Incomplete;

    // Nullable: OTP-only users may never set a password.
    public string? PasswordHash { get; set; }
    public string? ProfileImageUrl { get; set; }

    public string PreferredLanguage { get; set; } = "ar";

    public bool RememberMe { get; set; }
    public bool FaceIdEnabled { get; set; }
    public bool BiometricEnabled { get; set; }


    public bool IsEmailVerified { get; set; }
    public bool IsPhoneVerified { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    // When they last closed the app (their last hub connection went away). For "آخر ظهور".
    public DateTimeOffset? LastSeenAt { get; set; }
}
