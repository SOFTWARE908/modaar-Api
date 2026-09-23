using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Services;

namespace modaar.api.Features.Users.Controllers;

[ApiController]
[Route("api/users/me/security")]
[Authorize]
[Produces("application/json")]
public class SecurityController : ControllerBase
{
    private readonly ISecuritySettingsService _settings;

    public SecurityController(ISecuritySettingsService settings) => _settings = settings;

    [HttpGet]
    [ProducesResponseType(typeof(SecuritySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _settings.GetAsync(userId, ct)).ToActionResult();
    }

    // Partial update: an omitted flag is left as it was.
    [HttpPut]
    [ProducesResponseType(typeof(SecuritySettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateSecuritySettingsDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _settings.UpdateAsync(userId, request, ct)).ToActionResult();
    }
}
