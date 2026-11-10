using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Authentication.Dtos;

public record LoginRequestDto
{
    public required LoginMethod LoginMethod { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? PhoneNumber { get; init; }
    public string? CountryCode { get; init; }
    public string? NationalId { get; init; }
}
