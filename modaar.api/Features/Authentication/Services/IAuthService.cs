using modaar.api.Common.Auth;
using modaar.api.Features.Authentication.Dtos;

namespace modaar.api.Features.Authentication.Services;

public interface IAuthService
{
    Task<AuthResult<AuthSuccessResponseDto>> RegisterAsync(RegisterRequestDto request, CancellationToken ct);

    Task<AuthResult<AuthSuccessResponseDto>> LoginWithPasswordAsync(string email, string password, CancellationToken ct);

    Task<AuthResult<SendOtpResponseDto>> RequestPhoneOtpAsync(string phoneNumber, string countryCode, CancellationToken ct);
    Task<AuthResult<SendOtpResponseDto>> RequestNationalIdOtpAsync(string nationalId, CancellationToken ct);

    Task<AuthResult<ResendOtpResponseDto>> ResendPhoneOtpAsync(string phoneNumber, CancellationToken ct);
    Task<AuthResult<ResendOtpResponseDto>> ResendNationalIdOtpAsync(string nationalId, CancellationToken ct);

    Task<AuthResult<AuthSuccessResponseDto>> VerifyOtpAsync(string identifier, string otpCode, CancellationToken ct);

    Task<AuthResult<RefreshTokenResponseDto>> RefreshTokenAsync(string refreshToken, CancellationToken ct);

    Task<AuthResult<UserProfileDto>> GetProfileAsync(Guid userId, CancellationToken ct);
}
