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
    private readonly IBuildingRuleService _rules;

    public BuildingsController(IBuildingService buildings, IBuildingRuleService rules)
    {
        _buildings = buildings;
        _rules = rules;
    }

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

    // "أنظمة المبنى" for an owner or broker with units here. Tenants read them by unit:
    // GET /api/properties/{propertyId}/rules.
    [HttpGet("{buildingId:guid}/rules")]
    [ProducesResponseType(typeof(BuildingRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRules(Guid buildingId, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.GetForBuildingAsync(buildingId, c.UserId, c.Role, ct)).ToActionResult();
    }

    // The tenant's way in ("خدمات أخرى" → «أنظمة المبنى»): rules of the building this unit is in.
    // Tenant with an active lease on it, its owner, or the broker running it.
    [HttpGet("~/api/properties/{propertyId:guid}/rules")]
    [ProducesResponseType(typeof(BuildingRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUnitRules(Guid propertyId, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.GetForPropertyAsync(propertyId, c.UserId, c.Role, ct)).ToActionResult();
    }

    // Changing rules: only whoever added the building (canEdit). Each returns the whole rules screen.
    // The rule goes at the end of its category; categories come from GET /api/building-rule-categories.
    [HttpPost("{buildingId:guid}/rules")]
    [ProducesResponseType(typeof(BuildingRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddRule(Guid buildingId, [FromBody] AddBuildingRuleDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.AddRuleAsync(buildingId, c.UserId, c.Role, request, ct)).ToActionResult();
    }

    [HttpDelete("{buildingId:guid}/rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(BuildingRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRule(Guid buildingId, Guid ruleId, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.DeleteRuleAsync(buildingId, ruleId, c.UserId, c.Role, ct)).ToActionResult();
    }

    // "أرقام مهمة" under the rules.
    [HttpPost("{buildingId:guid}/rules/contacts")]
    [ProducesResponseType(typeof(BuildingRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddContact(Guid buildingId, [FromBody] AddBuildingContactDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.AddContactAsync(buildingId, c.UserId, c.Role, request, ct)).ToActionResult();
    }

    [HttpDelete("{buildingId:guid}/rules/contacts/{contactId:guid}")]
    [ProducesResponseType(typeof(BuildingRulesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteContact(Guid buildingId, Guid contactId, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.DeleteContactAsync(buildingId, contactId, c.UserId, c.Role, ct)).ToActionResult();
    }

    // ── Rule categories: one list for every building ──

    // Active categories, for the owner's "add a rule" picker. Any signed-in user.
    [HttpGet("~/api/building-rule-categories")]
    [ProducesResponseType(typeof(List<BuildingRuleCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRuleCategories(CancellationToken ct) =>
        Ok(await _rules.ListCategoriesAsync(ct));

    // Admin: everything, inactive included.
    [HttpGet("~/api/building-rule-categories/admin")]
    [ProducesResponseType(typeof(List<BuildingRuleCategoryAdminDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListRuleCategoriesForAdmin(CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.ListCategoriesForAdminAsync(c.Role, ct)).ToActionResult();
    }

    [HttpPost("~/api/building-rule-categories")]
    [ProducesResponseType(typeof(BuildingRuleCategoryAdminDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRuleCategory([FromBody] CreateBuildingRuleCategoryDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var result = await _rules.CreateCategoryAsync(c.Role, request, ct);
        return result.Success ? CreatedAtAction(nameof(ListRuleCategoriesForAdmin), null, result.Value) : result.ToActionResult();
    }

    [HttpPatch("~/api/building-rule-categories/{id:guid}")]
    [ProducesResponseType(typeof(BuildingRuleCategoryAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRuleCategory(Guid id, [FromBody] UpdateBuildingRuleCategoryDto request, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        return (await _rules.UpdateCategoryAsync(id, c.Role, request, ct)).ToActionResult();
    }

    // Deactivates; rules already written under it keep showing.
    [HttpDelete("~/api/building-rule-categories/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRuleCategory(Guid id, CancellationToken ct)
    {
        if (Caller() is not { } c) return NoCaller();
        var result = await _rules.DeleteCategoryAsync(id, c.Role, ct);
        return result.Success ? NoContent() : result.ToActionResult();
    }

    private (Guid UserId, Features.Authentication.Enums.AccountType Role)? Caller() =>
        User.GetUserId() is { } id && User.GetActiveRole() is { } role ? (id, role) : null;

    private IActionResult NoCaller() =>
        User.GetUserId() is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.")
            : Problem(statusCode: StatusCodes.Status403Forbidden, title: "Role not selected",
                detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");
}
