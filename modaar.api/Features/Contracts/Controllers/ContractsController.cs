using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Contracts.Dtos;
using modaar.api.Features.Contracts.Services;

namespace modaar.api.Features.Contracts.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
[Produces("application/json")]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contracts;

    public ContractsController(IContractService contracts) => _contracts = contracts;

    // Scoped by account type: tenants see their own leases, owners see their properties',
    // brokers see the ones they manage.
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<ContractDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] ContractListQueryDto query, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _contracts.ListAsync(userId, query, ct)).ToActionResult();
    }

    [HttpGet("{contractId:guid}")]
    [ProducesResponseType(typeof(ContractDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid contractId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _contracts.GetDetailsAsync(contractId, userId, ct)).ToActionResult();
    }

    // Redirects to the stored document. 404 until one has been uploaded — no generator exists.
    [HttpGet("{contractId:guid}/pdf")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPdf(Guid contractId, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _contracts.GetPdfUrlAsync(contractId, userId, ct);
        return result.Success ? Redirect(result.Value!) : result.ToActionResult();
    }

    // Tenant only, and only inside the renewal window.
    [HttpPost("{contractId:guid}/renew")]
    [ProducesResponseType(typeof(ContractRequestHistoryItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Renew(
        Guid contractId, [FromBody] RenewContractRequestDto? request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        return (await _contracts.RequestRenewalAsync(
            contractId, userId, request ?? new RenewContractRequestDto(), ct)).ToActionResult();
    }

    // An owner ends the lease outright and gets 204. A tenant files a termination request and
    // gets 200 with the pending row.
    [HttpPost("{contractId:guid}/end")]
    [ProducesResponseType(typeof(ContractRequestHistoryItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> End(
        Guid contractId, [FromBody] EndContractRequestDto request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } userId)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

        var result = await _contracts.EndAsync(contractId, userId, request, ct);

        if (!result.Success)
            return result.ToActionResult();

        return result.Value is null ? NoContent() : Ok(result.Value);
    }
}
