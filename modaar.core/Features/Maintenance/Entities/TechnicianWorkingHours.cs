namespace modaar.api.Features.Maintenance.Entities;

// When a technician works on one day of the week, e.g. Sunday 09:00–21:00. One row per day they
// work; a day with no row is a day off. A technician with no rows at all has no schedule set —
// they can still be booked at any time, but have no "next available" slot to show.
public class TechnicianWorkingHours
{
    public Guid Id { get; set; }
    public Guid TechnicianId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}
