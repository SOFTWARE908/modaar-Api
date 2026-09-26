using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Users.Dtos;
using modaar.api.Persistence;
using modaar.api.Common.Localization;
using modaar.api.Features.Maintenance;
namespace modaar.api.Features.Maintenance.Services;

public sealed class MaintenanceService : IMaintenanceService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly IRequestLanguage _language;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        ModaarDbContext db,
        TimeProvider time,
        IRequestLanguage language,
        ILogger<MaintenanceService> logger)
    {
        _db = db;
        _time = time;
        _language = language;
        _logger = logger;
    }

    public async Task<Result<PagedResultDto<MaintenanceRequestListItemDto>>> ListAsync(
        Guid callerUserId, MaintenanceListQueryDto query, CancellationToken ct)
    {
        var caller = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted)
            .Select(u => new { u.AccountType })
            .FirstOrDefaultAsync(ct);

        if (caller is null)
            return Result<PagedResultDto<MaintenanceRequestListItemDto>>.Fail(
                AppErrorCode.NotFound, "User not found.");

        var requests = _db.MaintenanceRequests.AsNoTracking();

        // The scoping rule is the authorization. Everything below it is just filtering.
        requests = caller.AccountType switch
        {
            AccountType.Tenant => requests.Where(m => m.TenantUserId == callerUserId),

            AccountType.Owner => requests.Where(m =>
                _db.Properties.Any(p => p.Id == m.PropertyId && p.OwnerUserId == callerUserId)),

            AccountType.Broker => requests.Where(m =>
                _db.Properties.Any(p => p.Id == m.PropertyId && p.BrokerId != null &&
                    _db.Brokers.Any(b => b.Id == p.BrokerId && b.UserId == callerUserId))),

            // A technician sees their own queue. Not a client role today, but the alternative is
            // returning every request in the system.
            _ => requests.Where(m =>
                _db.Technicians.Any(t => t.Id == m.AssignedTechnicianId && t.UserId == callerUserId))
        };

        if (query.Status is { Count: > 0 })
            requests = requests.Where(m => query.Status.Contains(m.Status));

        if (query.ServiceType is { } serviceType)
            requests = requests.Where(m => m.ServiceType == serviceType);

        if (query.PropertyId is { } propertyId)
            requests = requests.Where(m => m.PropertyId == propertyId);

        if (query.FromDate is { } from)
        {
            var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            requests = requests.Where(m => m.CreatedAt >= fromUtc);
        }

        if (query.ToDate is { } to)
        {
            // Inclusive of the whole end day.
            var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            requests = requests.Where(m => m.CreatedAt < toUtc);
        }

        var totalCount = await requests.CountAsync(ct);

        var rows = await requests
            .OrderByDescending(m => m.CreatedAt)
            .Skip(query.SkipCount)
            .Take(query.MaxResultCount)
            .Select(m => new
            {
                m.Id,
                m.RequestNumber,
                m.ServiceType,
                m.Status,
                m.CreatedAt,
                m.ScheduledVisitDate,
                m.ScheduledTimeSlot,
                m.PreferredVisitDate,
                m.PreferredTimeSlot,
                Technician = _db.Technicians
                    .Where(t => t.Id == m.AssignedTechnicianId)
                    .Select(t => new
                    {
                        t.Id,
                        t.RoleEn,
                        t.RoleAr,
                        t.RatingAverage,
                        Name = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault(),
                        ImageUrl = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault()
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = rows.Select(m =>
        {
            var date = m.ScheduledVisitDate ?? m.PreferredVisitDate;
            var slot = m.ScheduledTimeSlot ?? m.PreferredTimeSlot;

            return new MaintenanceRequestListItemDto
            {
                Id = m.Id,
                RequestCode = m.RequestNumber,
                Title = MaintenanceServiceTypeNames.Title(m.ServiceType, _language),
                ServiceType = m.ServiceType,
                Status = m.Status,
                CreatedAt = m.CreatedAt,
                VisitSummary = $"{date.ToString("d MMMM yyyy", _language.Culture)} · " +
                   $"{MaintenanceTimeSlotNames.Label(slot, _language)}",
                AssignedTechnician = m.Technician is null ? null : new TechnicianDto
                {
                    Id = m.Technician.Id,
                    Name = m.Technician.Name ?? string.Empty,
                    Role = _language.Pick(m.Technician.RoleAr, m.Technician.RoleEn),
                    ImageUrl = m.Technician.ImageUrl,
                    AverageRating = m.Technician.RatingAverage
                }
            };
        }).ToList();

        return Result<PagedResultDto<MaintenanceRequestListItemDto>>.Ok(
            new PagedResultDto<MaintenanceRequestListItemDto> { Items = items, TotalCount = totalCount });
    }

    public async Task<Result<MaintenanceRequestDetailsDto>> GetDetailsAsync(
        Guid requestId, Guid callerUserId, CancellationToken ct)
    {
        var request = await _db.MaintenanceRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == requestId, ct);

        if (request is null)
            return Result<MaintenanceRequestDetailsDto>.Fail(AppErrorCode.NotFound, "Request not found.");

        var property = await _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == request.PropertyId)
            .Select(p => new { p.Id, p.Title, p.OwnerUserId, p.BrokerId })
            .FirstOrDefaultAsync(ct);

        if (property is null)
            return Result<MaintenanceRequestDetailsDto>.Fail(AppErrorCode.NotFound, "Property not found.");

        var isTenant = request.TenantUserId == callerUserId;
        var isOwner = property.OwnerUserId == callerUserId;
        var isBroker = property.BrokerId is { } brokerId &&
            await _db.Brokers.AnyAsync(b => b.Id == brokerId && b.UserId == callerUserId, ct);
        var isTechnician = request.AssignedTechnicianId is { } techId &&
            await _db.Technicians.AnyAsync(t => t.Id == techId && t.UserId == callerUserId, ct);

        if (!isTenant && !isOwner && !isBroker && !isTechnician)
            return Result<MaintenanceRequestDetailsDto>.Fail(
                AppErrorCode.Forbidden, "You do not have access to this request.");

        var technician = request.AssignedTechnicianId is null ? null : await _db.Technicians
            .AsNoTracking()
            .Where(t => t.Id == request.AssignedTechnicianId)
            .Select(t => new TechnicianDto
            {
                Id = t.Id,
                Name = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                Role = _language.Pick(t.RoleAr, t.RoleEn),
                ImageUrl = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.ProfileImageUrl).FirstOrDefault(),
                AverageRating = t.RatingAverage
            })
            .FirstOrDefaultAsync(ct);

        var attachments = await _db.MaintenanceAttachments
            .AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.Url)
            .ToListAsync(ct);

        var stars = await _db.MaintenanceRatings
            .AsNoTracking()
            .Where(r => r.RequestId == requestId)
            .Select(r => (int?)r.Stars)
            .FirstOrDefaultAsync(ct);

        // The tenant block is for whoever is looking at someone else's request; showing a tenant
        // their own name and number back is noise.
        TenantDto? tenant = null;
        if (!isTenant)
        {
            tenant = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == request.TenantUserId)
                .Select(u => new TenantDto
                {
                    Id = u.Id,
                    Name = u.FullName,
                    AvatarUrl = u.ProfileImageUrl,
                    PhoneNumber = u.CountryCode + u.PhoneNumber
                })
                .FirstOrDefaultAsync(ct);
        }

        var visitDate = request.ScheduledVisitDate ?? request.PreferredVisitDate;
        var visitSlot = request.ScheduledTimeSlot ?? request.PreferredTimeSlot;
        var today = SaudiTime.Today(_time);

        return Result<MaintenanceRequestDetailsDto>.Ok(new MaintenanceRequestDetailsDto
        {
            Id = request.Id,
            RequestNumber = request.RequestNumber,
            Status = request.Status,
            RejectedBy = request.RejectedBy,
            RejectionReason = request.RejectionReason,
            VisitDate = visitDate,
            VisitDayIsTomorrow = visitDate.DayNumber == today.DayNumber + 1,
            VisitTimeRange = VisitSlots.Format(visitSlot),
            VisitTimePeriod = visitSlot,
            AssignedTechnician = technician,
            TenantRatingStars = stars,
            ProblemDescription = request.ProblemDescription,
            ServiceType = request.ServiceType,
            AttachmentImageUrls = attachments,
            PropertyId = property.Id,
            PropertyTitle = property.Title,
            Tenant = tenant
        });
    }

    public async Task<Result<CreateMaintenanceRequestResponseDto>> CreateAsync(
        Guid tenantUserId, CreateMaintenanceRequestDto request, CancellationToken ct)
    {
        var accountType = await _db.Users
            .Where(u => u.Id == tenantUserId && !u.IsDeleted && u.IsActive)
            .Select(u => (AccountType?)u.AccountType)
            .FirstOrDefaultAsync(ct);

        if (accountType is null)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(AppErrorCode.NotFound, "User not found.");

        // Only a tenant raises maintenance — the account-type matrix in the spec is explicit.
        if (accountType != AccountType.Tenant)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(
                AppErrorCode.Forbidden, "Only a tenant can raise a maintenance request.");

        // The lease is what grants access to the unit, so the request is anchored to it rather
        // than to a property id the caller chose.
        var leases = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.TenantUserId == tenantUserId && c.Status == ContractStatus.Active)
            .Select(c => new { c.Id, c.PropertyId })
            .ToListAsync(ct);

        if (leases.Count == 0)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(
                AppErrorCode.Forbidden, "You have no active contract.");

        var lease = request.PropertyId is { } requestedProperty
            ? leases.FirstOrDefault(l => l.PropertyId == requestedProperty)
            : leases.Count == 1 ? leases[0] : null;

        if (lease is null)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(
                AppErrorCode.ValidationFailed,
                request.PropertyId is null
                    ? "You hold more than one active contract; specify propertyId."
                    : "You have no active contract on that property.");

        var attachmentIds = request.AttachmentIds ?? [];
        List<MaintenanceAttachment> attachments = [];

        if (attachmentIds.Count > 0)
        {
            // Only unclaimed uploads by this same caller. Otherwise anyone could attach someone
            // else's file by guessing an id, or re-attach a file already on another request.
            attachments = await _db.MaintenanceAttachments
                .Where(a => attachmentIds.Contains(a.Id)
                         && a.RequestId == null
                         && a.UploadedByUserId == tenantUserId)
                .ToListAsync(ct);

            if (attachments.Count != attachmentIds.Count)
                return Result<CreateMaintenanceRequestResponseDto>.Fail(
                    AppErrorCode.ValidationFailed, "One or more attachments are unknown or already used.");
        }

        var now = _time.GetUtcNow();

        var maintenanceRequest = new MaintenanceRequest
        {
            Id = Guid.NewGuid(),
            RequestNumber = await GenerateRequestNumberAsync(ct),
            PropertyId = lease.PropertyId,
            ContractId = lease.Id,
            TenantUserId = tenantUserId,
            ServiceType = request.ServiceType,
            ProblemDescription = request.Description.Trim(),
            Status = MaintenanceRequestStatus.Pending,
            PreferredVisitDate = request.PreferredVisitDate,
            PreferredTimeSlot = request.VisitTimeSlot,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.MaintenanceRequests.Add(maintenanceRequest);

        foreach (var attachment in attachments)
            attachment.RequestId = maintenanceRequest.Id;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Maintenance request {Number} created by {UserId} on property {PropertyId}.",
            maintenanceRequest.RequestNumber, tenantUserId, lease.PropertyId);

        return Result<CreateMaintenanceRequestResponseDto>.Ok(new CreateMaintenanceRequestResponseDto
        {
            Id = maintenanceRequest.Id,
            RequestCode = maintenanceRequest.RequestNumber
        });
    }

    // "D-654321" to match the client. Random rather than sequential so the code doesn't leak how
    // many requests the platform has handled; retried on the unique index.
    private async Task<string> GenerateRequestNumberAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = $"D-{Random.Shared.Next(100_000, 1_000_000)}";
            if (!await _db.MaintenanceRequests.AnyAsync(m => m.RequestNumber == candidate, ct))
                return candidate;
        }

        // Six digits is only a million codes; fall back to something that cannot collide rather
        // than fail the request.
        return $"D-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }
}
