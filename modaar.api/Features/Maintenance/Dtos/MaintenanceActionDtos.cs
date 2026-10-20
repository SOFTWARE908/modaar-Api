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
        public MaintenancePriorityDto? Priority { get; init; }

        // The job needs pricing before work starts; the tenant sees "awaiting quote" until one
        // is submitted.
        public bool RequiresQuote { get; init; }
    }

    // Owner or managing broker prices the work. Resubmitting replaces the previous quote.
    public record SubmitMaintenanceQuoteDto
    {
        public required decimal PartsCost { get; init; }
        public required decimal LaborCost { get; init; }
        public required MaintenanceCostBearerDto CostBearer { get; init; }
        public string? Description { get; init; }
    }

    // Assigns (or replaces) the technician and fixes the visit to a date and an exact time, as the
    // broker picks them on the assign screen.
    public record AssignTechnicianDto
    {
        public required Guid TechnicianId { get; init; }
        public required DateOnly VisitDate { get; init; }
        public required TimeOnly VisitTime { get; init; }
    }

    // "تغيير الموعد": same technician, new date and time.
    public record RescheduleMaintenanceVisitDto
    {
        public required DateOnly VisitDate { get; init; }
        public required TimeOnly VisitTime { get; init; }
    }

    // Alias so the DTO file doesn't pull the Enums namespace into every consumer.
    public enum MaintenanceTimeSlotDto { Morning, Afternoon, Evening }
    public enum MaintenancePriorityDto { Normal, Urgent }
    public enum MaintenanceCostBearerDto { Owner, Tenant, Broker }
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

    public sealed class SubmitMaintenanceQuoteValidator : AbstractValidator<SubmitMaintenanceQuoteDto>
    {
        public SubmitMaintenanceQuoteValidator()
        {
            RuleFor(x => x.PartsCost).GreaterThanOrEqualTo(0).LessThan(1_000_000);
            RuleFor(x => x.LaborCost).GreaterThanOrEqualTo(0).LessThan(1_000_000);
            RuleFor(x => x).Must(x => x.PartsCost + x.LaborCost > 0)
                .WithMessage("A quote must have a non-zero total.");
            RuleFor(x => x.CostBearer).IsInEnum();
            RuleFor(x => x.Description).MaximumLength(1000);
        }
    }

    public sealed class AssignTechnicianValidator : AbstractValidator<AssignTechnicianDto>
    {
        public AssignTechnicianValidator()
        {
            RuleFor(x => x.TechnicianId).NotEmpty();
        }
    }

    public sealed class AcceptMaintenanceRequestValidator: AbstractValidator<AcceptMaintenanceRequestDto>
    {
        public AcceptMaintenanceRequestValidator()
        {
            RuleFor(x => x.ScheduledTimeSlot!.Value)
                .IsInEnum()
                .When(x => x.ScheduledTimeSlot is not null);

            RuleFor(x => x.Priority!.Value)
                .IsInEnum()
                .When(x => x.Priority is not null);

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
