using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Announcements.Dtos;
using modaar.api.Features.Announcements.Services;
using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Announcements.Controllers;

// Owner → own tenants notices (elevator maintenance, stairwell cleaning…). Sent once, never edited.
[ApiController]
[Authorize]
[Produces("application/json")]
public class AnnouncementsController : ControllerBase
{
    private readonly IAnnouncementService _announcements;

    public AnnouncementsController(IAnnouncementService announcements) => _announcements = announcements;

    // ── Owner ──

    // scope: building (buildingId) / units (propertyIds) / allProperties. Goes to every tenant
    // with an active lease on the owner's units in scope; 409 if there are none.
    [HttpPost("api/announcements")]
    [ProducesResponseType(typeof(SentAnnouncementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Send([FromBody] SendAnnouncementDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.SendAsync(c.UserId, c.Role, request, ct)).ToActionResult();
    }

    // With how many tenants got it and how many pressed "تم الاطلاع".
    [HttpGet("api/announcements/sent")]
    [ProducesResponseType(typeof(PagedResultDto<SentAnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListSent([FromQuery] PagedRequestDto query, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.ListSentAsync(c.UserId, c.Role, query, ct)).ToActionResult();
    }

    // Plus each recipient and when they acknowledged.
    [HttpGet("api/announcements/sent/{id:guid}")]
    [ProducesResponseType(typeof(SentAnnouncementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSent(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.GetSentAsync(id, c.UserId, c.Role, ct)).ToActionResult();
    }

    // ── Tenant ──

    [HttpGet("api/announcements")]
    [ProducesResponseType(typeof(PagedResultDto<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListMine([FromQuery] PagedRequestDto query, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.ListMineAsync(c.UserId, c.Role, query, ct)).ToActionResult();
    }

    [HttpGet("api/announcements/{id:guid}")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMine(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.GetMineAsync(id, c.UserId, c.Role, ct)).ToActionResult();
    }

    // "تم الاطلاع". Pressing it again keeps the first time.
    [HttpPost("api/announcements/{id:guid}/acknowledge")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.AcknowledgeAsync(id, c.UserId, c.Role, ct)).ToActionResult();
    }

    // ── Categories ──

    // Active categories for the "new announcement" picker. Any signed-in user.
    [HttpGet("api/announcement-categories")]
    [ProducesResponseType(typeof(List<AnnouncementCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCategories(CancellationToken ct) =>
        Ok(await _announcements.ListCategoriesAsync(ct));

    // Admin: everything, inactive included.
    [HttpGet("api/announcement-categories/admin")]
    [ProducesResponseType(typeof(List<AnnouncementCategoryAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListCategoriesForAdmin(CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.ListCategoriesForAdminAsync(c.Role, ct)).ToActionResult();
    }

    [HttpPost("api/announcement-categories")]
    [ProducesResponseType(typeof(AnnouncementCategoryAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateAnnouncementCategoryDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var result = await _announcements.CreateCategoryAsync(c.Role, request, ct);
        return result.Success ? CreatedAtAction(nameof(ListCategoriesForAdmin), null, result.Value) : result.ToActionResult();
    }

    [HttpPatch("api/announcement-categories/{id:guid}")]
    [ProducesResponseType(typeof(AnnouncementCategoryAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateAnnouncementCategoryDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _announcements.UpdateCategoryAsync(id, c.Role, request, ct)).ToActionResult();
    }

    // Deactivates; announcements already sent keep their category.
    [HttpDelete("api/announcement-categories/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var result = await _announcements.DeleteCategoryAsync(id, c.Role, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    private (Guid UserId, AccountType Role)? Caller() =>
        User.GetUserId() is { } id && User.GetActiveRole() is { } role ? (id, role) : null;

    private IActionResult NoCaller() =>
        User.GetUserId() is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.")
            : Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
}
