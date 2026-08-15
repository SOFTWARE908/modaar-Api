using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;

namespace modaar.api.Common.Errors;

public static class AuthResultExtensions
{
    public static IActionResult ToActionResult<T>(this AuthResult<T> result)
    {
        if (result.Success)
            return new OkObjectResult(result.Value);

        var (status, title) = MapStatus(result.ErrorCode!.Value);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = result.Error
        };
        problem.Extensions["errorCode"] = result.ErrorCode.Value.ToString();

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static (int Status, string Title) MapStatus(AuthErrorCode code) => code switch
    {
        AuthErrorCode.InvalidCredentials       => (StatusCodes.Status401Unauthorized,  "Invalid credentials"),
        AuthErrorCode.UserNotFound             => (StatusCodes.Status404NotFound,      "User not found"),
        AuthErrorCode.AccountInactive          => (StatusCodes.Status403Forbidden,     "Account inactive"),
        AuthErrorCode.AccountTypeNotAllowed    => (StatusCodes.Status400BadRequest,    "Account type not allowed"),
        AuthErrorCode.DuplicateRegistration    => (StatusCodes.Status409Conflict,      "Duplicate registration"),
        AuthErrorCode.OtpExpired               => (StatusCodes.Status410Gone,          "OTP expired"),
        AuthErrorCode.OtpInvalid               => (StatusCodes.Status400BadRequest,    "Invalid OTP"),
        AuthErrorCode.OtpMaxAttempts           => (StatusCodes.Status429TooManyRequests, "Too many OTP attempts"),
        AuthErrorCode.OtpResendCooldown        => (StatusCodes.Status429TooManyRequests, "OTP resend cooldown"),
        AuthErrorCode.InvalidRefreshToken      => (StatusCodes.Status401Unauthorized,  "Invalid refresh token"),
        AuthErrorCode.UnsupportedLoginMethod   => (StatusCodes.Status400BadRequest,    "Unsupported login method"),
        _                                      => (StatusCodes.Status500InternalServerError, "Authentication error")
    };
}
