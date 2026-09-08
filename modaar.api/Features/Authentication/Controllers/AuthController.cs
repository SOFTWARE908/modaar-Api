using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Errors;
using modaar.api.Features.Authentication.Dtos;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Authentication.Services;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Services;

namespace modaar.api.Features.Authentication.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IProfileService _profiles;

    public AuthController(IAuthService auth, IProfileService profiles)
    {
        _auth = auth;
        _profiles = profiles;
    }

    // The only way into the system. There is no separate registration step: an unrecognised mobile
    // number simply becomes a new account once its OTP is confirmed.
    //
    // Switches on LoginMethod:
    //   PhoneOtp/NationalIdOtp → SendOtpResponseDto, then POST /verify-otp for the tokens
    //   EmailPassword          → AuthSuccessResponseDto (dormant: nothing sets a password yet)
    [HttpPost("login")]
    [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthSuccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        switch (request.LoginMethod)
        {
            case LoginMethod.PhoneOtp:
                if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.CountryCode))
                    return BadField("PhoneNumber and CountryCode are required for PhoneOtp login.");
                return (await _auth.RequestPhoneOtpAsync(request.PhoneNumber, request.CountryCode, ct)).ToActionResult();

            // Only resolves an account that has already had a national ID added to its profile;
            // unlike a mobile number, it cannot create one.
            case LoginMethod.NationalIdOtp:
                if (string.IsNullOrWhiteSpace(request.NationalId))
                    return BadField("NationalId is required for NationalIdOtp login.");
                return (await _auth.RequestNationalIdOtpAsync(request.NationalId, ct)).ToActionResult();

            case LoginMethod.EmailPassword:
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return BadField("Email and Password are required for EmailPassword login.");
                return (await _auth.LoginWithPasswordAsync(request.Email, request.Password, ct)).ToActionResult();

            default:
                return BadField("Unsupported login method.");
        }
    }

    // Confirms a mobile number and returns the tokens, creating the account if the number is new.
    // The response says which happened via isNewUser / profileStatus.
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(AuthSuccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDto request, CancellationToken ct)
    {
        var result = await _auth.VerifyOtpAsync(request.Identifier, request.OtpCode, ct);
        return result.ToActionResult();
    }

    [HttpPost("resend-otp")]
    [ProducesResponseType(typeof(ResendOtpResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequestDto request, CancellationToken ct)
    {
        var result = request.LoginMethod switch
        {
            LoginMethod.PhoneOtp      => await _auth.ResendPhoneOtpAsync(request.Identifier, ct),
            LoginMethod.NationalIdOtp => await _auth.ResendNationalIdOtpAsync(request.Identifier, ct),
            _ => AuthResult<ResendOtpResponseDto>.Fail(
                AuthErrorCode.UnsupportedLoginMethod,
                "OTP cannot be resent for this login method.")
        };
        return result.ToActionResult();
    }

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(RefreshTokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken ct)
    {
        var result = await _auth.RefreshTokenAsync(request.RefreshToken, ct);
        return result.ToActionResult();
    }

    // Convenience alias for GET /api/profile.
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _profiles.GetAsync(userId, ct)).ToActionResult();
    }

    private IActionResult BadField(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
