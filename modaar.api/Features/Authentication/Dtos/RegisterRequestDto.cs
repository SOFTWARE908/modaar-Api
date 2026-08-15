using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Authentication.Dtos;

public record RegisterRequestDto
{
    // Technician is reserved for future use and must be rejected on this endpoint.
    public required AccountType AccountType { get; init; }
    public required string UserName { get; init; }
    public required string NationalId { get; init; }
    public required string PhoneNumber { get; init; }
    public required string CountryCode { get; init; }
    public required string Email { get; init; }
    public string? InviteCode { get; init; }
}
