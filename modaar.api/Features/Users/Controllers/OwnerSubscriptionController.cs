using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Users.Services;

namespace modaar.api.Features.Users.Controllers;

// The owner's subscription plan (selfManaged / managed / fullyManaged) and auto-approve limit.
// The broker sets it for their clients; the owner can only see it.
[ApiController]
[Authorize]
[Produces("application/json")]
public class OwnerSubscriptionController : ControllerBase
{
    private readonly IOwnerSubscriptionService _subscriptions;

    public OwnerSubscriptionController(IOwnerSubscriptionService subscriptions) => _subscriptions = subscriptions;

    [HttpGet("api/brokers/me/clients/{ownerUserId:guid}/subscription")]
    [ProducesResponseType(typeof(OwnerSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForBroker(Guid ownerUserId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _subscriptions.GetForBrokerAsync(userId, ownerUserId, ct)).ToActionResult();
    }

    // Applies to requests raised from now on; open requests keep the plan they started under.
    [HttpPut("api/brokers/me/clients/{ownerUserId:guid}/subscription")]
    [ProducesResponseType(typeof(OwnerSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateForBroker(
        Guid ownerUserId, [FromBody] UpdateOwnerSubscriptionDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _subscriptions.UpdateForBrokerAsync(userId, ownerUserId, request, ct)).ToActionResult();
    }

    [HttpGet("api/profile/subscription")]
    [ProducesResponseType(typeof(OwnerSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _subscriptions.GetMineAsync(userId, ct)).ToActionResult();
    }
}
