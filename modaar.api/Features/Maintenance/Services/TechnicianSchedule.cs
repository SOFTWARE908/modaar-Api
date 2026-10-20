using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Time;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

// A technician's week: working hours, and the visits already booked. Every visit blocks two
// hours. Free slots are two-hour blocks from the start of the working day that fit before its end,
// don't overlap a booked visit, and haven't started yet (Riyadh time).
public sealed class TechnicianCalendar
{
    public static readonly TimeSpan VisitLength = TimeSpan.FromHours(2);

    private readonly List<TechnicianWorkingHours> _hours;
    private readonly List<(DateTime Start, DateTime End, Guid RequestId)> _busy;

    public TechnicianCalendar(List<TechnicianWorkingHours> hours, List<(DateTime Start, DateTime End, Guid RequestId)> busy)
    {
        _hours = hours;
        _busy = busy;
    }

    public bool HasSchedule => _hours.Count > 0;

    public IEnumerable<DateTime> FreeSlots(DateOnly from, int days, DateTime now)
    {
        for (var day = from; day < from.AddDays(days); day = day.AddDays(1))
        {
            foreach (var hours in _hours.Where(h => h.DayOfWeek == day.DayOfWeek))
            {
                var end = day.ToDateTime(hours.End);
                for (var start = day.ToDateTime(hours.Start); start + VisitLength <= end; start += VisitLength)
                    if (start > now && !Overlaps(start, null))
                        yield return start;
            }
        }
    }

    // Null when the technician can take a visit starting then; otherwise why not.
    public string? WhyNot(DateTime start, Guid? ignoreRequestId)
    {
        if (HasSchedule)
        {
            var fits = _hours.Any(h => h.DayOfWeek == start.DayOfWeek
                && TimeOnly.FromDateTime(start) >= h.Start
                && start + VisitLength <= DateOnly.FromDateTime(start).ToDateTime(h.End));
            if (!fits)
                return "That time is outside the technician's working hours.";
        }

        return Overlaps(start, ignoreRequestId) ? "The technician already has a visit at that time." : null;
    }

    private bool Overlaps(DateTime start, Guid? ignoreRequestId) =>
        _busy.Any(b => b.RequestId != ignoreRequestId && start < b.End && b.Start < start + VisitLength);
}

public static class TechnicianSchedule
{
    // Calendars for several technicians at once, with visits from `from` onwards.
    public static async Task<Dictionary<Guid, TechnicianCalendar>> LoadAsync(
        ModaarDbContext db, IReadOnlyCollection<Guid> technicianIds, DateOnly from, CancellationToken ct)
    {
        var hours = await db.TechnicianWorkingHours.AsNoTracking()
            .Where(h => technicianIds.Contains(h.TechnicianId))
            .ToListAsync(ct);

        // Visits that still hold the technician's time: accepted, scheduled with an exact time.
        var visits = await db.MaintenanceRequests.AsNoTracking()
            .Where(m => m.AssignedTechnicianId != null && technicianIds.Contains(m.AssignedTechnicianId.Value)
                     && m.Status == MaintenanceRequestStatus.Accepted
                     && m.ScheduledVisitDate != null && m.ScheduledVisitTime != null
                     && m.ScheduledVisitDate >= from.AddDays(-1))
            .Select(m => new { m.Id, Technician = m.AssignedTechnicianId!.Value, Date = m.ScheduledVisitDate!.Value, Time = m.ScheduledVisitTime!.Value })
            .ToListAsync(ct);

        return technicianIds.ToDictionary(id => id, id => new TechnicianCalendar(
            hours.Where(h => h.TechnicianId == id).ToList(),
            visits.Where(v => v.Technician == id)
                .Select(v => (Start: v.Date.ToDateTime(v.Time), End: v.Date.ToDateTime(v.Time) + TechnicianCalendar.VisitLength, RequestId: v.Id))
                .ToList()));
    }

    public static DateTime RiyadhNow(TimeProvider time) =>
        TimeZoneInfo.ConvertTime(time.GetUtcNow(), SaudiTime.Zone).DateTime;

    // A Riyadh wall-clock time as a DateTimeOffset (+03:00) for the API.
    public static DateTimeOffset ToOffset(DateTime riyadh) =>
        new(riyadh, SaudiTime.Zone.GetUtcOffset(riyadh));
}
