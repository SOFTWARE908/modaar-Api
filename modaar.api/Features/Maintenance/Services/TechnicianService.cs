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
}

public sealed class TechnicianService : ITechnicianService
{
    private readonly ModaarDbContext _db;
    private readonly IRequestLanguage _language;

    public TechnicianService(ModaarDbContext db, IRequestLanguage language)
    {
        _db = db;
        _language = language;
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
                CompletedJobsCount = r.CompletedJobs
            })
            .ToList();

        return Result<List<TechnicianListItemDto>>.Ok(items);
    }
}
