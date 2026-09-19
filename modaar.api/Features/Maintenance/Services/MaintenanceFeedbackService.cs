using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

public interface IMaintenanceFeedbackService
{
    Task<Result<bool>> SubmitRatingAsync(
        Guid requestId, Guid tenantUserId, MaintenanceRatingSubmitDto request, CancellationToken ct);

    Task<Result<bool>> SubmitNeedHelpAsync(
        Guid requestId, Guid tenantUserId, NeedHelpRequestDto request, CancellationToken ct);
}

public sealed class MaintenanceFeedbackService : IMaintenanceFeedbackService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<MaintenanceFeedbackService> _logger;

    public MaintenanceFeedbackService(
        ModaarDbContext db, TimeProvider time, ILogger<MaintenanceFeedbackService> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public async Task<Result<bool>> SubmitRatingAsync(
        Guid requestId, Guid tenantUserId, MaintenanceRatingSubmitDto request, CancellationToken ct)
    {
        var maintenanceRequest = await _db.MaintenanceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == requestId, ct);

        if (maintenanceRequest is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Request not found.");

        // Only the tenant who raised it rates it — an owner rating their own contractor would
        // make the technician's average meaningless.
        if (maintenanceRequest.TenantUserId != tenantUserId)
            return Result<bool>.Fail(AppErrorCode.Forbidden, "Only the tenant can rate this request.");

        if (maintenanceRequest.Status != MaintenanceRequestStatus.Completed)
            return Result<bool>.Fail(AppErrorCode.Conflict, "This request is not completed yet.");

        if (maintenanceRequest.AssignedTechnicianId is null)
            return Result<bool>.Fail(AppErrorCode.Conflict, "No technician was assigned to this request.");

        // One rating per request. A resubmission edits the existing one rather than being
        // rejected, so a tenant can fix a mis-tap.
        var rating = await _db.MaintenanceRatings.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        var now = _time.GetUtcNow();

        if (rating is null)
        {
            rating = new MaintenanceRating
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                // Snapshot: the request's assignment could change later, but this rating belongs
                // to whoever actually did the work.
                TechnicianId = maintenanceRequest.AssignedTechnicianId,
                CreatedAt = now
            };
            _db.MaintenanceRatings.Add(rating);
        }

        rating.Stars = request.Stars;
        rating.Comment = request.Comment?.Trim();
        rating.TagKeys = request.TagKeys?.ToList() ?? [];
        rating.TipAmount = request.PresetTipSar ?? request.CustomTipSar;
        rating.UpdatedAt = now;

        // The rating and the rollup must never be visible out of step.
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await RecalculateTechnicianRollupAsync(maintenanceRequest.AssignedTechnicianId.Value, ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Maintenance {Number} rated {Stars} by {UserId}.",
            maintenanceRequest.RequestNumber, request.Stars, tenantUserId);

        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> SubmitNeedHelpAsync(
        Guid requestId, Guid tenantUserId, NeedHelpRequestDto request, CancellationToken ct)
    {
        var maintenanceRequest = await _db.MaintenanceRequests
            .AsNoTracking()
            .Where(m => m.Id == requestId)
            .Select(m => new { m.Id, m.TenantUserId, m.Status, m.RequestNumber })
            .FirstOrDefaultAsync(ct);

        if (maintenanceRequest is null)
            return Result<bool>.Fail(AppErrorCode.NotFound, "Request not found.");

        if (maintenanceRequest.TenantUserId != tenantUserId)
            return Result<bool>.Fail(AppErrorCode.Forbidden, "Only the tenant can raise help on this request.");

        // Escalating a request that was never accepted, or was rejected, has nothing to chase.
        if (maintenanceRequest.Status is MaintenanceRequestStatus.Rejected
                                      or MaintenanceRequestStatus.Cancelled)
            return Result<bool>.Fail(AppErrorCode.Conflict,
                $"This request is {maintenanceRequest.Status}; there is nothing to escalate.");

        // The filtered unique index enforces this too, but a 409 reads better than a constraint
        // violation surfacing as a 500.
        var alreadyOpen = await _db.MaintenanceNeedHelpTickets
            .AnyAsync(t => t.RequestId == requestId && t.Status == NeedHelpStatus.Open, ct);

        if (alreadyOpen)
            return Result<bool>.Fail(AppErrorCode.Conflict,
                "You already have an open help ticket on this request.");

        var now = _time.GetUtcNow();

        _db.MaintenanceNeedHelpTickets.Add(new MaintenanceNeedHelp
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            RaisedByUserId = tenantUserId,
            IssueType = request.IssueType,
            Description = request.Description?.Trim(),
            ContactMethod = request.ContactMethod,
            Status = NeedHelpStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogWarning("Help raised on maintenance {Number} by {UserId}: {IssueType}, contact via {Method}.",
            maintenanceRequest.RequestNumber, tenantUserId, request.IssueType, request.ContactMethod);

        return Result<bool>.Ok(true);
    }

    // Re-derived from the ratings themselves rather than nudged, so an edited rating lands on the
    // same number a fresh count would. Mirrors the broker review rollup.
    private Task RecalculateTechnicianRollupAsync(Guid technicianId, CancellationToken ct) =>
        _db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE t
               SET t.RatingsCount  = x.RatingsCount,
                   t.RatingAverage = x.RatingAverage,
                   t.UpdatedAt     = SYSDATETIMEOFFSET()
              FROM Technicians t
             CROSS APPLY (
                   SELECT COUNT(*) AS RatingsCount,
                          -- AVG over an int column does integer division in SQL Server; without
                          -- the cast, 4 and 5 average to 4.
                          ISNULL(AVG(CAST(r.Stars AS decimal(3,2))), 0) AS RatingAverage
                     FROM MaintenanceRatings r
                    WHERE r.TechnicianId = t.Id
                   ) x
             WHERE t.Id = {technicianId};
            """, ct);
}
