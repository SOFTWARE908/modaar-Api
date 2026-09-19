using FluentValidation;

namespace modaar.api.Features.Maintenance.Dtos
{
    public record RejectMaintenanceRequestDto
    {
        public required string Reason { get; init; }
    }

    public record MaintenanceInquiryDto
    {
        public required string Message { get; init; }
    }

    public record DelegateMaintenanceRequestDto
    {
        // Optional: defaults to the brokerage already managing the property. Supplying one that
        // isn't that brokerage is rejected — delegating to an unrelated broker would hand them
        // access to a property they have no relationship with.
        public Guid? BrokerId { get; init; }
    }

    // Accept optionally schedules the visit. Omitted means the tenant's preferred date and slot
    // stand as agreed.
    public record AcceptMaintenanceRequestDto
    {
        public DateOnly? ScheduledVisitDate { get; init; }
        public MaintenanceTimeSlotDto? ScheduledTimeSlot { get; init; }
        public Guid? TechnicianId { get; init; }
    }

    // Alias so the DTO file doesn't pull the Enums namespace into every consumer.
    public enum MaintenanceTimeSlotDto { Morning, Afternoon, Evening }
}

namespace modaar.api.Features.Maintenance.Validation
{
    using modaar.api.Features.Maintenance.Dtos;

    public sealed class RejectMaintenanceRequestValidator : AbstractValidator<RejectMaintenanceRequestDto>
    {
        public RejectMaintenanceRequestValidator()
        {
            // A rejection the tenant can't understand is worse than none; require a real reason.
            RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(1000);
        }
    }

    public sealed class MaintenanceInquiryValidator : AbstractValidator<MaintenanceInquiryDto>
    {
        public MaintenanceInquiryValidator()
        {
            RuleFor(x => x.Message).NotEmpty().MinimumLength(3).MaximumLength(2000);
        }
    }

    public sealed class AcceptMaintenanceRequestValidator : AbstractValidator<AcceptMaintenanceRequestDto>
    {
        public AcceptMaintenanceRequestValidator()
        {
            RuleFor(x => x.ScheduledTimeSlot!.Value)
                .IsInEnum()
                .When(x => x.ScheduledTimeSlot is not null);

            // A date without a slot is ambiguous and a slot without a date is meaningless.
            RuleFor(x => x.ScheduledTimeSlot)
                .NotNull().When(x => x.ScheduledVisitDate is not null)
                .WithMessage("A time slot is required when a visit date is supplied.");

            RuleFor(x => x.ScheduledVisitDate)
                .NotNull().When(x => x.ScheduledTimeSlot is not null)
                .WithMessage("A visit date is required when a time slot is supplied.");
        }
    }
}
