using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using modaar.api.Common.Auth;
using modaar.api.Common.Errors;
using modaar.api.Features.Authentication.Dtos;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Authentication.Services;

namespace modaar.api.Features.Authentication.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthSuccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        return result.ToActionResult();
    }

    // Switches on LoginMethod:
    //   EmailPassword          → AuthSuccessResponseDto (tokens)
    //   PhoneOtp/NationalIdOtp → SendOtpResponseDto (OTP triggered)
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthSuccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendOtpResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        switch (request.LoginMethod)
        {
            case LoginMethod.EmailPassword:
                if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
                    return BadField("Email and Password are required for EmailPassword login.");
                return (await _auth.LoginWithPasswordAsync(request.Email, request.Password, ct)).ToActionResult();

            case LoginMethod.PhoneOtp:
                if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.CountryCode))
                    return BadField("PhoneNumber and CountryCode are required for PhoneOtp login.");
                return (await _auth.RequestPhoneOtpAsync(request.PhoneNumber, request.CountryCode, ct)).ToActionResult();

            case LoginMethod.NationalIdOtp:
                if (string.IsNullOrWhiteSpace(request.NationalId))
                    return BadField("NationalId is required for NationalIdOtp login.");
                return (await _auth.RequestNationalIdOtpAsync(request.NationalId, ct)).ToActionResult();

            default:
                return BadField("Unsupported login method.");
        }
    }

    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(AuthSuccessResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
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

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _auth.GetProfileAsync(userId, ct);
        return result.ToActionResult();
    }

    private IActionResult BadField(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
