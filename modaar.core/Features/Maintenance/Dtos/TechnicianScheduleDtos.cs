using FluentValidation;

namespace modaar.api.Features.Maintenance.Dtos
{
    public record WorkingHoursDto
    {
        // "sunday" … "saturday"
        public required DayOfWeek DayOfWeek { get; init; }

        // "09:00", "21:00"
        public required TimeOnly Start { get; init; }
        public required TimeOnly End { get; init; }
    }

    // Replaces the whole week: send every working day; days not sent are days off.
    public record SetWorkingHoursDto
    {
        public required IReadOnlyList<WorkingHoursDto> Days { get; init; }
    }

    // One day's free two-hour slots, as start times: "اليوم 4:00 م / غداً 10:00 ص".
    public record TechnicianAvailabilityDayDto
    {
        public required DateOnly Date { get; init; }
        public required IReadOnlyList<TimeOnly> Slots { get; init; }
    }

    public record TechnicianAvailabilityDto
    {
        public required Guid TechnicianId { get; init; }

        // False when the technician has no working hours set: no slots to offer, but they can
        // still be booked at any free time.
        public required bool HasSchedule { get; init; }

        // Every visit blocks this many minutes (120).
        public required int VisitLengthMinutes { get; init; }

        public required IReadOnlyList<TechnicianAvailabilityDayDto> Days { get; init; }
    }
}

namespace modaar.api.Features.Maintenance.Validation
{
    using modaar.api.Features.Maintenance.Dtos;

    public sealed class SetWorkingHoursValidator : AbstractValidator<SetWorkingHoursDto>
    {
        public SetWorkingHoursValidator()
        {
            RuleFor(x => x.Days).NotNull();
            RuleForEach(x => x.Days).ChildRules(d =>
            {
                d.RuleFor(x => x.DayOfWeek).IsInEnum();
                d.RuleFor(x => x).Must(x => x.End > x.Start).WithMessage("End must be after start.");
            });
            RuleFor(x => x.Days)
                .Must(days => days.Select(d => d.DayOfWeek).Distinct().Count() == days.Count)
                .WithMessage("Each day at most once.");
        }
    }
}
