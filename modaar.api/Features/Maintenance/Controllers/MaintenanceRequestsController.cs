using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using modaar.api.Common.Auth;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Services;

namespace modaar.api.Features.Maintenance.Validation
{
    public sealed class CreateMaintenanceRequestValidator : AbstractValidator<CreateMaintenanceRequestDto>
    {
        public CreateMaintenanceRequestValidator()
        {
            RuleFor(x => x.ServiceType).IsInEnum();
            RuleFor(x => x.VisitTimeSlot).IsInEnum();

            RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);

            // No date check against "today" here — the validator has no clock and no time zone,
            // and a Riyadh-local comparison belongs in the service.
            RuleFor(x => x.PreferredVisitDate).NotEqual(default(DateOnly));

            RuleFor(x => x.AttachmentIds!)
                .Must(ids => ids.Count <= VisitSlots.MaxAttachments)
                .When(x => x.AttachmentIds is not null)
                .WithMessage($"At most {VisitSlots.MaxAttachments} attachments are allowed.")
                .Must(ids => ids.Distinct().Count() == ids.Count)
                .When(x => x.AttachmentIds is not null)
                .WithMessage("Attachment ids must be unique.");
        }
    }
}

namespace modaar.api.Features.Maintenance.Controllers
{
    [ApiController]
    [Route("api/maintenance-requests")]
    [Authorize]
    [Produces("application/json")]
    public class MaintenanceRequestsController : ControllerBase
    {
        private readonly IMaintenanceService _maintenance;
        private readonly IMaintenanceAttachmentService _attachments;

        public MaintenanceRequestsController(
            IMaintenanceService maintenance, IMaintenanceAttachmentService attachments)
        {
            _maintenance = maintenance;
            _attachments = attachments;
        }

        // Scoped by account type: tenants see their own, owners see their properties', brokers
        // see the ones they manage.
        [HttpGet]
        [ProducesResponseType(typeof(PagedResultDto<MaintenanceRequestListItemDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List([FromQuery] MaintenanceListQueryDto query, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

            return (await _maintenance.ListAsync(userId, query, ct)).ToActionResult();
        }

        [HttpGet("{requestId:guid}")]
        [ProducesResponseType(typeof(MaintenanceRequestDetailsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid requestId, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

            return (await _maintenance.GetDetailsAsync(requestId, userId, ct)).ToActionResult();
        }

        // Tenant only. The property is resolved from the caller's active lease.
        [HttpPost]
        [ProducesResponseType(typeof(CreateMaintenanceRequestResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create(
            [FromBody] CreateMaintenanceRequestDto request, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

            var result = await _maintenance.CreateAsync(userId, request, ct);
            if (!result.Success)
                return result.ToActionResult();

            return CreatedAtAction(nameof(Get), new { requestId = result.Value!.Id }, result.Value);
        }

        // Upload first, then pass the returned ids as attachmentIds on create. An upload that is
        // never attached stays orphaned and is cleaned up out of band.
        [HttpPost("attachments")]
        [RequestSizeLimit(25 * 1024 * 1024)]
        [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UploadAttachment(IFormFile file, CancellationToken ct)
        {
            if (User.GetUserId() is not { } userId)
                return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Invalid token subject.");

            if (file is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "No file was supplied.");

            return (await _attachments.UploadAsync(userId, file, ct)).ToActionResult();
        }
    }
}
