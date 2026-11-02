using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Services;

namespace modaar.api.Features.Users.Controllers;

[ApiController]
[Route("api/brokers")]
[Authorize]
[Produces("application/json")]
public class BrokersController : ControllerBase
{
    private readonly IBrokerService _brokers;

    public BrokersController(IBrokerService brokers) => _brokers = brokers;

    // Visible to any signed-in user: the account-type matrix gives owner, tenant and broker
    // all "broker profile view".
    [HttpGet("{brokerId:guid}")]
    [ProducesResponseType(typeof(BrokerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid brokerId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _brokers.GetProfileAsync(brokerId, userId, ct)).ToActionResult();
    }

    // Newest first. Carries isReviewedByMe so the client knows whether to offer writing one.
    [HttpGet("{brokerId:guid}/reviews")]
    [ProducesResponseType(typeof(BrokerReviewsPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListReviews(
        Guid brokerId, [FromQuery] PagedRequestDto query, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _brokers.ListReviewsAsync(brokerId, userId, query, ct)).ToActionResult();
    }

    // The broker's own profile.
    [HttpGet("me")]
    [ProducesResponseType(typeof(BrokerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");
        if (User.GetActiveRole() is not { } role)
            return NoRole();

        return (await _brokers.GetMineAsync(userId, role, ct)).ToActionResult();
    }

    // Subtitle, about, year started, services (codes) and coverage areas. Only what's sent changes.
    [HttpPatch("me")]
    [ProducesResponseType(typeof(BrokerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateMine([FromBody] UpdateMyBrokerProfileDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");
        if (User.GetActiveRole() is not { } role)
            return NoRole();

        return (await _brokers.UpdateMineAsync(userId, role, request, ct)).ToActionResult();
    }

    // "سوق الوسطاء": ?search= (name, or a district/city) &service= (code) &sort= rating (default) | units.
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<BrokerListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] BrokersRequestDto query, CancellationToken ct) =>
        (await _brokers.ListAsync(query, ct)).ToActionResult();

    // Owner with a unit this broker runs. Four criteria and a comment; sending again edits it.
    [HttpPut("{brokerId:guid}/reviews/mine")]
    [ProducesResponseType(typeof(BrokerReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(Guid brokerId, [FromBody] SubmitBrokerReviewDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } role)
            return NoRole();

        return (await _brokers.SubmitReviewAsync(brokerId, userId, role, request, ct)).ToActionResult();
    }

    private IActionResult NoRole() =>
        Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
            detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
}
