using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Agreements.Dtos;
using modaar.api.Features.Agreements.Services;
using modaar.api.Features.Authentication.Enums;

namespace modaar.api.Features.Agreements.Controllers;

// Management agreements (تفويض) between an owner and a broker. The owner sends one (units +
// maintenance auto-approve limit); the broker accepts or rejects it.
[ApiController]
[Authorize]
[Produces("application/json")]
public class AgreementsController : ControllerBase
{
    private readonly IManagementAgreementService _agreements;

    public AgreementsController(IManagementAgreementService agreements) => _agreements = agreements;

    // Owner. propertyIds omitted → all their units.
    [HttpPost("api/agreements")]
    [ProducesResponseType(typeof(AgreementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAgreementDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _agreements.CreateAsync(c.UserId, c.Role, request, ct)).ToActionResult();
    }

    // The same, from the broker's profile.
    [HttpPost("api/brokers/{brokerId:guid}/management-requests")]
    [ProducesResponseType(typeof(AgreementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestManagement(Guid brokerId, [FromBody] RequestManagementDto? request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var body = new CreateAgreementDto
        {
            BrokerId = brokerId,
            PropertyIds = request?.PropertyIds,
            AutoApproveLimit = request?.AutoApproveLimit
        };
        return (await _agreements.CreateAsync(c.UserId, c.Role, body, ct)).ToActionResult();
    }

    // The caller's agreements: an owner's with their brokers, a broker's with their owners.
    // Pending first, then active, then ended.
    [HttpGet("api/agreements")]
    [ProducesResponseType(typeof(PagedResultDto<AgreementListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] AgreementsRequestDto query, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _agreements.ListAsync(c.UserId, c.Role, query, ct)).ToActionResult();
    }

    [HttpGet("api/agreements/{id:guid}")]
    [ProducesResponseType(typeof(AgreementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _agreements.GetAsync(id, c.UserId, c.Role, ct)).ToActionResult();
    }

    // Broker. Runs the units from today.
    [HttpPost("api/agreements/{id:guid}/accept")]
    [ProducesResponseType(typeof(AgreementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _agreements.AcceptAsync(id, c.UserId, c.Role, ct)).ToActionResult();
    }

    // Broker, on a pending one.
    [HttpPost("api/agreements/{id:guid}/reject")]
    [ProducesResponseType(typeof(AgreementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] AgreementReasonDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _agreements.RejectAsync(id, c.UserId, c.Role, request.Reason, ct)).ToActionResult();
    }

    // Owner on a pending one → withdrawn. Either side on an active one → cancelled, every unit
    // leaves today.
    [HttpPost("api/agreements/{id:guid}/cancel")]
    [ProducesResponseType(typeof(AgreementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] AgreementReasonDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _agreements.CancelAsync(id, c.UserId, c.Role, request.Reason, ct)).ToActionResult();
    }

    // Owner. A new pending mandate to the same broker; omitted units and limit are copied.
    [HttpPost("api/agreements/{id:guid}/renew")]
    [ProducesResponseType(typeof(AgreementDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Renew(Guid id, [FromBody] RenewAgreementDto? request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _agreements.RenewAsync(id, c.UserId, c.Role, request ?? new RenewAgreementDto(), ct)).ToActionResult();
    }

    private (Guid UserId, AccountType Role)? Caller() =>
        User.GetUserId() is { } id && User.GetActiveRole() is { } role ? (id, role) : null;

    private IActionResult NoCaller() =>
        User.GetUserId() is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.")
            : Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
}
