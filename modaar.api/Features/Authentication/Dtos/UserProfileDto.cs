using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Authentication.Dtos;

public record UserProfileDto
{
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public required string PhoneNumber { get; init; }
    public required string CountryCode { get; init; }
    public required string NationalId { get; init; }
    public required AccountType AccountType { get; init; }
    public string? ProfileImageUrl { get; init; }
}
