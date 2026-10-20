using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

public interface ITechnicianService
{
    Task<Result<List<TechnicianListItemDto>>> ListAsync(Guid callerUserId, Guid? serviceTypeId, CancellationToken ct);
    Task<Result<List<WorkingHoursDto>>> GetWorkingHoursAsync(Guid technicianId, CancellationToken ct);
    Task<Result<List<WorkingHoursDto>>> SetWorkingHoursAsync(Guid technicianId, Guid callerUserId, SetWorkingHoursDto request, CancellationToken ct);
    Task<Result<TechnicianAvailabilityDto>> GetAvailabilityAsync(Guid technicianId, DateOnly? from, int days, CancellationToken ct);
}

public sealed class TechnicianService : ITechnicianService
{
    // How far ahead "أقرب موعد" and the slot list look.
    private const int LookAheadDays = 14;

    private readonly ModaarDbContext _db;
    private readonly IRequestLanguage _language;
    private readonly TimeProvider _time;

    public TechnicianService(ModaarDbContext db, IRequestLanguage language, TimeProvider time)
    {
        _db = db;
        _language = language;
        _time = time;
    }

    // Only technicians the caller could actually assign — the same rule assign-technician checks:
    // independents, plus a brokerage's own staff for that brokerage (a broker caller) or for the
    // brokerages managing the caller's properties (an owner caller).
    public async Task<Result<List<TechnicianListItemDto>>> ListAsync(
        Guid callerUserId, Guid? serviceTypeId, CancellationToken ct)
    {
        var brokerIds = await _db.Brokers
            .Where(b => b.UserId == callerUserId)
            .Select(b => b.Id)
            .Union(_db.Properties
                .Where(p => p.OwnerUserId == callerUserId && p.BrokerId != null)
                .Select(p => p.BrokerId!.Value))
            .ToListAsync(ct);

        var technicians = _db.Technicians
            .AsNoTracking()
            .Where(t => t.IsAvailable && (t.BrokerId == null || brokerIds.Contains(t.BrokerId.Value)));

        if (serviceTypeId is { } typeId)
            technicians = technicians.Where(t =>
                _db.TechnicianServiceTypes.Any(s => s.TechnicianId == t.Id && s.ServiceTypeId == typeId));

        var rows = await technicians
            .Select(t => new
            {
                t.Id,
                t.RoleAr,
                t.RoleEn,
                t.RatingAverage,
                t.RatingsCount,
                User = _db.Users.Where(u => u.Id == t.UserId)
                    .Select(u => new { u.FullName, u.ProfileImageUrl })
                    .FirstOrDefault(),
                Specialties = _db.TechnicianServiceTypes
                    .Where(s => s.TechnicianId == t.Id)
                    .Join(_db.MaintenanceServiceTypes, s => s.ServiceTypeId, st => st.Id,
                        (s, st) => new { st.NameAr, st.NameEn, st.SortOrder })
                    .ToList(),
                CompletedJobs = _db.MaintenanceRequests.Count(m =>
                    m.AssignedTechnicianId == t.Id && m.Status == MaintenanceRequestStatus.Completed)
            })
            .ToListAsync(ct);

        var now = TechnicianSchedule.RiyadhNow(_time);
        var calendars = await TechnicianSchedule.LoadAsync(_db, rows.Select(r => r.Id).ToList(), DateOnly.FromDateTime(now), ct);

        // Best first, the way the design ranks them.
        var items = rows
            .OrderByDescending(r => r.RatingAverage)
            .ThenByDescending(r => r.CompletedJobs)
            .Select(r => new TechnicianListItemDto
            {
                Id = r.Id,
                Name = r.User?.FullName ?? string.Empty,
                AvatarUrl = r.User?.ProfileImageUrl,
                Role = _language.Pick(r.RoleAr, r.RoleEn),
                Specialties = r.Specialties.OrderBy(s => s.SortOrder).Select(s => _language.Pick(s.NameAr, s.NameEn)).ToList(),
                AverageRating = r.RatingAverage,
                RatingsCount = r.RatingsCount,
                CompletedJobsCount = r.CompletedJobs,
                NextAvailableAt = calendars[r.Id].FreeSlots(DateOnly.FromDateTime(now), LookAheadDays, now)
                    .Select(s => (DateTimeOffset?)TechnicianSchedule.ToOffset(s)).FirstOrDefault()
            })
            .ToList();

        return Result<List<TechnicianListItemDto>>.Ok(items);
    }

    public async Task<Result<List<WorkingHoursDto>>> GetWorkingHoursAsync(Guid technicianId, CancellationToken ct)
    {
        if (!await _db.Technicians.AnyAsync(t => t.Id == technicianId, ct))
            return Result<List<WorkingHoursDto>>.Fail(AppErrorCode.NotFound, "Technician not found.");

        return Result<List<WorkingHoursDto>>.Ok(await HoursAsync(technicianId, ct));
    }

    // The technician sets their own week, or the brokerage they work for does.
    public async Task<Result<List<WorkingHoursDto>>> SetWorkingHoursAsync(
        Guid technicianId, Guid callerUserId, SetWorkingHoursDto request, CancellationToken ct)
    {
        var technician = await _db.Technicians.AsNoTracking()
            .Where(t => t.Id == technicianId)
            .Select(t => new { t.UserId, BrokerUserId = _db.Brokers.Where(b => b.Id == t.BrokerId).Select(b => (Guid?)b.UserId).FirstOrDefault() })
            .FirstOrDefaultAsync(ct);

        if (technician is null)
            return Result<List<WorkingHoursDto>>.Fail(AppErrorCode.NotFound, "Technician not found.");

        if (technician.UserId != callerUserId && technician.BrokerUserId != callerUserId)
            return Result<List<WorkingHoursDto>>.Fail(AppErrorCode.Forbidden, "Only the technician or their brokerage can set their hours.");

        await _db.TechnicianWorkingHours.Where(h => h.TechnicianId == technicianId).ExecuteDeleteAsync(ct);
        _db.TechnicianWorkingHours.AddRange(request.Days.Select(d => new Entities.TechnicianWorkingHours
        {
            Id = Guid.NewGuid(),
            TechnicianId = technicianId,
            DayOfWeek = d.DayOfWeek,
            Start = d.Start,
            End = d.End
        }));
        await _db.SaveChangesAsync(ct);

        return Result<List<WorkingHoursDto>>.Ok(await HoursAsync(technicianId, ct));
    }

    public async Task<Result<TechnicianAvailabilityDto>> GetAvailabilityAsync(
        Guid technicianId, DateOnly? from, int days, CancellationToken ct)
    {
        if (!await _db.Technicians.AnyAsync(t => t.Id == technicianId, ct))
            return Result<TechnicianAvailabilityDto>.Fail(AppErrorCode.NotFound, "Technician not found.");

        var now = TechnicianSchedule.RiyadhNow(_time);
        var start = from ?? DateOnly.FromDateTime(now);
        days = Math.Clamp(days, 1, LookAheadDays);

        var calendar = (await TechnicianSchedule.LoadAsync(_db, [technicianId], start, ct))[technicianId];
        var slots = calendar.FreeSlots(start, days, now).ToList();

        return Result<TechnicianAvailabilityDto>.Ok(new TechnicianAvailabilityDto
        {
            TechnicianId = technicianId,
            HasSchedule = calendar.HasSchedule,
            VisitLengthMinutes = (int)TechnicianCalendar.VisitLength.TotalMinutes,
            Days = Enumerable.Range(0, days)
                .Select(i => start.AddDays(i))
                .Select(d => new TechnicianAvailabilityDayDto
                {
                    Date = d,
                    Slots = slots.Where(s => DateOnly.FromDateTime(s) == d).Select(TimeOnly.FromDateTime).ToList()
                })
                .ToList()
        });
    }

    private async Task<List<WorkingHoursDto>> HoursAsync(Guid technicianId, CancellationToken ct) =>
        (await _db.TechnicianWorkingHours.AsNoTracking().Where(h => h.TechnicianId == technicianId).ToListAsync(ct))
            .OrderBy(h => h.DayOfWeek)
            .Select(h => new WorkingHoursDto { DayOfWeek = h.DayOfWeek, Start = h.Start, End = h.End })
            .ToList();
}
