using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
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
    private readonly IBrokerClientService _clients;

    public OwnerSubscriptionController(IOwnerSubscriptionService subscriptions, IBrokerClientService clients)
    {
        _subscriptions = subscriptions;
        _clients = clients;
    }

    // "عملائي" (B-5): owners whose units the broker manages, with units / rented / vacant and plan.
    // Sorted by units, most first. ?search= matches name or phone.
    [HttpGet("api/brokers/me/clients")]
    [ProducesResponseType(typeof(PagedResultDto<BrokerClientListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListClients([FromQuery] BrokerClientsRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } role)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

        return (await _clients.ListAsync(userId, role, request, ct)).ToActionResult();
    }

    // Client screen (B-6): the owner, totals, this month's money, open maintenance and their
    // buildings — all over this broker's units only. 404 when the owner isn't this broker's client.
    [HttpGet("api/brokers/me/clients/{ownerUserId:guid}")]
    [ProducesResponseType(typeof(BrokerClientDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClient(Guid ownerUserId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        if (User.GetActiveRole() is not { } role)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

        return (await _clients.GetAsync(userId, role, ownerUserId, ct)).ToActionResult();
    }

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
