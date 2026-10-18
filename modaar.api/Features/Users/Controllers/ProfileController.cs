using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Errors;
using modaar.api.Common.Results;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Services;

namespace modaar.api.Features.Users.Controllers;

// Accounts are created with nothing but a confirmed mobile number, so the profile is filled in
// here, after login, rather than up front.
[ApiController]
[Route("api/profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IProfileService _profiles;

    public ProfileController(IProfileService profiles) => _profiles = profiles;

    [HttpGet]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _profiles.GetAsync(userId, ct)).ToActionResult();
    }

    // Partial update: omitted fields are left alone. Supplying a full name marks the profile complete.
    [HttpPut]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromBody] UpdateProfileRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _profiles.UpdateAsync(userId, request, ct)).ToActionResult();
    }

    // multipart/form-data with one "file" field. Replaces the current picture and returns the
    // updated profile with the new profileImageUrl.
    [HttpPost("avatar")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (file is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No file was supplied.");

        return (await _profiles.UploadAvatarAsync(userId, file, ct)).ToActionResult();
    }
}
