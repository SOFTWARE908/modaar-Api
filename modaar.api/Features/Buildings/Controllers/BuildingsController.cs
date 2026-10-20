using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Buildings.Dtos;
using modaar.api.Features.Buildings.Services;
using modaar.api.Features.Properties.Dtos;

namespace modaar.api.Features.Buildings.Controllers;

// Buildings and the units inside them, for owners and brokers. Everything is scoped to the
// caller's own units in each building (see BuildingService).
[ApiController]
[Route("api/buildings")]
[Authorize]
[Produces("application/json")]
public class BuildingsController : ControllerBase
{
    private readonly IBuildingService _buildings;

    public BuildingsController(IBuildingService buildings) => _buildings = buildings;

    // "عقاراتي". ?vacantOnly=true → buildings with at least one empty unit.
    [HttpGet]
    [ProducesResponseType(typeof(List<BuildingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] bool vacantOnly, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _buildings.ListAsync(c.UserId, c.Role, vacantOnly, ct)).ToActionResult();
    }

    // The building screen: summary and units.
    [HttpGet("{buildingId:guid}")]
    [ProducesResponseType(typeof(BuildingDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid buildingId, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _buildings.GetAsync(buildingId, c.UserId, c.Role, ct)).ToActionResult();
    }

    [HttpPost]
    [ProducesResponseType(typeof(BuildingDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateBuildingDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _buildings.CreateAsync(c.UserId, c.Role, request, ct)).ToActionResult();
    }

    // Only whoever added the building (canEdit on the details).
    [HttpPatch("{buildingId:guid}")]
    [ProducesResponseType(typeof(BuildingDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid buildingId, [FromBody] UpdateBuildingDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _buildings.UpdateAsync(buildingId, c.UserId, c.Role, request, ct)).ToActionResult();
    }

    // Adds a unit inside the building. An owner adds their own; a broker names which client owns it.
    [HttpPost("{buildingId:guid}/units")]
    [ProducesResponseType(typeof(PropertyDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddUnit(Guid buildingId, [FromBody] AddBuildingUnitDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _buildings.AddUnitAsync(buildingId, c.UserId, c.Role, request, ct)).ToActionResult();
    }

    private (Guid UserId, Features.Authentication.Enums.AccountType Role)? Caller() =>
        User.GetUserId() is { } id && User.GetActiveRole() is { } role ? (id, role) : null;

    private IActionResult NoCaller() =>
        User.GetUserId() is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.")
            : Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
}
