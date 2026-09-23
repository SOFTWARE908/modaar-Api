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

    // Owner only. Records the ask; nothing here approves it or assigns the broker.
    [HttpPost("{brokerId:guid}/management-requests")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestManagement(
        Guid brokerId, [FromBody] BrokerManagementRequestDto? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        // An empty body means the whole portfolio.
        var result = await _brokers.RequestManagementAsync(
            brokerId, userId, request ?? new BrokerManagementRequestDto(), ct);

        return result.Success ? NoContent() : result.ToActionResult();
    }
}
