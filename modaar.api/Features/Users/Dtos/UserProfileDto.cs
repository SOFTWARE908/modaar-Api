using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Users.Dtos;

public record UserProfileDto
{
    // Always present: the account cannot exist without a confirmed mobile number.
    public required string PhoneNumber { get; init; }
    public required string CountryCode { get; init; }

    public required AccountType AccountType { get; init; }
    public required ProfileStatus ProfileStatus { get; init; }

    // Filled in after the first login; null until the user completes their profile.
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? NationalId { get; init; }
    public string? ProfileImageUrl { get; init; }
}
