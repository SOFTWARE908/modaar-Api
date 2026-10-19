using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Errors;
using modaar.api.Features.Authentication.Dtos;
using modaar.api.Features.Authentication.Services;

namespace modaar.api.Features.Authentication.Controllers;

// The signed-in user's phones. Biometric key registration today; push-token registration for
// notifications belongs here too.
[ApiController]
[Route("api/devices")]
[Authorize]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IDeviceService _devices;

    public DevicesController(IAuthService auth, IDeviceService devices)
    {
        _auth = auth;
        _devices = devices;
    }

    // Registers this phone for push notifications. Call after login and whenever Firebase
    // rotates the token (onTokenRefresh). Safe to call repeatedly.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _devices.RegisterPushTokenAsync(userId, request, ct);
        return result.Success ? NoContent() : Common.Results.ResultExtensions.ToActionResult(result);
    }

    // Called once when the user turns on Face ID / fingerprint login. Re-registering the same
    // device replaces its key.
    [HttpPost("biometric")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterBiometric(
        [FromBody] RegisterBiometricDeviceDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _auth.RegisterBiometricDeviceAsync(userId, request, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }
}
