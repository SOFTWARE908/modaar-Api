using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Users.Dtos;

public record UpdateProfileRequestDto
{
    // Supplying a full name and an account type is what marks the profile complete.
    public string? FullName { get; init; }
    public IReadOnlyList<AccountType>? AccountTypes { get; init; }
    // Optional extras; both must stay unique across active accounts.
    public string? Email { get; init; }
    public string? NationalId { get; init; }
    public string? ProfileImageUrl { get; init; }
    public string? Language { get; init; }

}
