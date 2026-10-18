using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Services;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Features.Maintenance.Controllers
{
    [ApiController]
    [Route("api/maintenance-service-types")]
    [Authorize]
    [Produces("application/json")]
    public class MaintenanceServiceTypesController : ControllerBase
    {
        private readonly IMaintenanceServiceTypeService _types;

        public MaintenanceServiceTypesController(IMaintenanceServiceTypeService types) => _types = types;

        // The picker. No role check: a tenant raising a request, an owner reading one and a broker
        // triaging one all need the same list.
        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyList<MaintenanceServiceTypeDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            var result = await _types.ListAsync(ct);
            return result.Success ? Ok(result.Value) : result.ToActionResult();
        }

        [HttpGet("admin")]
        [ProducesResponseType(typeof(IReadOnlyList<MaintenanceServiceTypeAdminDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> ListForAdmin(CancellationToken ct)
        {
            if (User.GetActiveRole() is not { } activeRole)
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Role not selected",
                    detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

            var result = await _types.ListForAdminAsync(activeRole, ct);
            return result.Success ? Ok(result.Value) : result.ToActionResult();
        }

        [HttpPost]
        [ProducesResponseType(typeof(MaintenanceServiceTypeAdminDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(
            [FromBody] CreateMaintenanceServiceTypeDto request, CancellationToken ct)
        {
            if (User.GetActiveRole() is not { } activeRole)
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Role not selected",
                    detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

            var result = await _types.CreateAsync(request, activeRole, ct);
            if (!result.Success)
                return result.ToActionResult();

            return CreatedAtAction(nameof(ListForAdmin), null, result.Value);
        }

        [HttpPatch("{serviceTypeId:guid}")]
        [ProducesResponseType(typeof(MaintenanceServiceTypeAdminDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            Guid serviceTypeId, [FromBody] UpdateMaintenanceServiceTypeDto request, CancellationToken ct)
        {
            if (User.GetActiveRole() is not { } activeRole)
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Role not selected",
                    detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

            var result = await _types.UpdateAsync(serviceTypeId, request, activeRole, ct);
            return result.Success ? Ok(result.Value) : result.ToActionResult();
        }

        // Deactivates rather than deletes — a category with requests against it can never be removed.
        [HttpDelete("{serviceTypeId:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Deactivate(Guid serviceTypeId, CancellationToken ct)
        {
            if (User.GetActiveRole() is not { } activeRole)
                return Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Role not selected",
                    detail: "Choose an account type with POST /api/auth/select-role before using this endpoint.");

            var result = await _types.DeactivateAsync(serviceTypeId, activeRole, ct);
            return result.Success ? NoContent() : result.ToActionResult();
        }
    }

}
