using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Dtos;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Notifications.Services;
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
    private readonly INotificationService _notifications;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(
        ModaarDbContext db,
        TimeProvider time,
        IRequestLanguage language,
        INotificationService notifications,
        ILogger<MaintenanceService> logger)
    {
        _db = db;
        _time = time;
        _language = language;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Result<PagedResultDto<MaintenanceRequestListItemDto>>> ListAsync(
        Guid callerUserId, MaintenanceListQueryDto query, AccountType activeRole, CancellationToken ct)
    {
        var caller = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted && u.AccountTypes.Contains(activeRole))
            .Select(u => new { u.AccountTypes })
            .FirstOrDefaultAsync(ct);

        if (caller is null)
            return Result<PagedResultDto<MaintenanceRequestListItemDto>>.Fail(
                AppErrorCode.NotFound, "User not found.");

        var requests = _db.MaintenanceRequests.AsNoTracking();

        // The scoping rule is the authorization. Everything below it is just filtering.
        requests = activeRole switch
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

        if (query.ServiceTypeId is { } serviceType)
            requests = requests.Where(m => m.ServiceTypeId == serviceType);

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

        var items = await MaintenanceListProjection.ToListItemsAsync(
            requests
                .OrderByDescending(m => m.CreatedAt)
                .Skip(query.SkipCount)
                .Take(query.MaxResultCount),
            _db, _language, ct);

        return Result<PagedResultDto<MaintenanceRequestListItemDto>>.Ok(
            new PagedResultDto<MaintenanceRequestListItemDto> { Items = items, TotalCount = totalCount });
    }

    public async Task<Result<MaintenanceRequestDetailsDto>> GetDetailsAsync(
        Guid requestId, Guid callerUserId, AccountType activeRole, CancellationToken ct)
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

        var isTenant = (request.TenantUserId == callerUserId && activeRole == AccountType.Tenant);
        var isOwner = (property.OwnerUserId == callerUserId && activeRole == AccountType.Owner);

        var isBroker = property.BrokerId is { } brokerId && activeRole == AccountType.Broker &&
            await _db.Brokers.AnyAsync(b => b.Id == brokerId && b.UserId == callerUserId, ct);

        var isTechnician = request.AssignedTechnicianId is { } techId && activeRole == AccountType.Technician &&
            await _db.Technicians.AnyAsync(t => t.Id == techId && t.UserId == callerUserId, ct);

        if (!isTenant && !isOwner && !isBroker && !isTechnician)
            return Result<MaintenanceRequestDetailsDto>.Fail(
                AppErrorCode.Forbidden, "You do not have access to this request.");

        var technician = request.AssignedTechnicianId is null ? null : await _db.Technicians
            .AsNoTracking()
            .Where(t => t.Id == request.AssignedTechnicianId)
            .Select(t => new
            {
                t.Id,
                t.RoleAr,
                t.RoleEn,
                t.RatingAverage,
                User = _db.Users.Where(u => u.Id == t.UserId)
                    .Select(u => new { u.FullName, u.ProfileImageUrl, u.CountryCode, u.PhoneNumber })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        var attachments = await _db.MaintenanceAttachments
            .AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.Url)
            .ToListAsync(ct);

        var rating = await _db.MaintenanceRatings
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);

        var actions = await _db.MaintenanceRequestActions
            .AsNoTracking()
            .Where(a => a.RequestId == requestId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new ActionRow(
                a.Type,
                a.ActorUserId,
                _db.Users.Where(u => u.Id == a.ActorUserId).Select(u => u.FullName).FirstOrDefault(),
                a.Message,
                a.CreatedAt))
            .ToListAsync(ct);

        var serviceType = await _db.MaintenanceServiceTypes
            .AsNoTracking()
            .Where(t => t.Id == request.ServiceTypeId)
            .Select(t => new { t.Id, t.Code, t.NameAr, t.NameEn, t.IconUrl, t.SortOrder })
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

        var (handledBy, handledByName) = await ResolveHandlerAsync(
            actions, property.OwnerUserId, property.BrokerId, ct);

        var visitDate = request.ScheduledVisitDate ?? request.PreferredVisitDate;
        var visitSlot = request.ScheduledTimeSlot ?? request.PreferredTimeSlot;
        var today = SaudiTime.Today(_time);

        var categoryName = serviceType is null ? string.Empty : _language.Pick(serviceType.NameAr, serviceType.NameEn);
        var stage = MaintenanceStages.Derive(
            request.Status, property.BrokerId is not null, request.QuoteRequired, request.QuotedAt is not null);

        return Result<MaintenanceRequestDetailsDto>.Ok(new MaintenanceRequestDetailsDto
        {
            Id = request.Id,
            RequestNumber = request.RequestNumber,
            Priority = request.Priority,
            Title = string.IsNullOrWhiteSpace(request.Title) ? categoryName : request.Title,
            CreatedAt = request.CreatedAt,
            Location = request.Location,
            Status = request.Status,
            Stage = stage,
            NextActionLabel = MaintenanceStages.NextActionLabel(
                stage, technician is not null, visitDate, rating is not null, _language),
            RejectedBy = request.RejectedBy,
            RejectionReason = request.RejectionReason,
            HandledBy = handledBy,
            HandledByName = handledByName,
            StatusHistory = BuildHistory(request, actions),
            VisitDate = visitDate,
            VisitDayIsTomorrow = visitDate is { } d && d.DayNumber == today.DayNumber + 1,
            VisitTimeRange = visitSlot is { } slot ? VisitSlots.Format(slot) : null,
            VisitTimePeriod = visitSlot,
            AssignedTechnician = technician is null ? null : new TechnicianDto
            {
                Id = technician.Id,
                Name = technician.User?.FullName ?? string.Empty,
                Role = _language.Pick(technician.RoleAr, technician.RoleEn),
                ImageUrl = technician.User?.ProfileImageUrl,
                AverageRating = technician.RatingAverage,
                PhoneNumber = technician.User is null ? null : technician.User.CountryCode + technician.User.PhoneNumber
            },
            Quote = request.QuotedAt is null ? null : new MaintenanceQuoteDto
            {
                PartsCost = request.QuotePartsCost ?? 0,
                LaborCost = request.QuoteLaborCost ?? 0,
                Total = (request.QuotePartsCost ?? 0) + (request.QuoteLaborCost ?? 0),
                CostBearer = request.QuoteCostBearer ?? MaintenanceCostBearer.Owner,
                Description = request.QuoteDescription
            },
            TenantRatingStars = rating?.Stars,
            Rating = rating is null ? null : ToRatingDto(rating),
            ProblemDescription = request.ProblemDescription,
            ServiceType = serviceType is null
                ? null!
                : new MaintenanceServiceTypeDto
                {
                    Id = serviceType.Id,
                    Code = serviceType.Code,
                    Name = categoryName,
                    IconUrl = serviceType.IconUrl,
                    SortOrder = serviceType.SortOrder
                },
            AttachmentImageUrls = attachments,
            PropertyId = property.Id,
            PropertyTitle = property.Title,
            Tenant = tenant
        });
    }

    public async Task<Result<CreateMaintenanceRequestResponseDto>> CreateAsync(
        Guid tenantUserId, CreateMaintenanceRequestDto request, AccountType activeRole, CancellationToken ct)
    {

        var accountType = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == tenantUserId && !u.IsDeleted)
            .Select(u => u.AccountTypes)
            .FirstOrDefaultAsync(ct);

        if (accountType is null)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(AppErrorCode.NotFound, "User not found.");

        // Only a tenant raises maintenance — the account-type matrix in the spec is explicit.
        if (!accountType.Contains(AccountType.Tenant) && activeRole != AccountType.Tenant)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(
                AppErrorCode.Forbidden, "Only a tenant can raise a maintenance request.");
        var serviceType = await _db.MaintenanceServiceTypes
            .AsNoTracking()
            .Where(t => t.Id == request.ServiceTypeId)
            .Select(t => new { t.Id, t.IsActive })
            .FirstOrDefaultAsync(ct);

        if (serviceType is null)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(
                AppErrorCode.NotFound, "Service type not found.");

        if (!serviceType.IsActive)
            return Result<CreateMaintenanceRequestResponseDto>.Fail(
                AppErrorCode.Conflict, "That service type is no longer available.");

        if (request.PreferredVisitDate is { } preferred && preferred < SaudiTime.Today(_time))
            return Result<CreateMaintenanceRequestResponseDto>.Fail(
                AppErrorCode.ValidationFailed, "The preferred visit date is in the past.");

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
            ServiceTypeId = request.ServiceTypeId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
            ProblemDescription = request.Description.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Status = MaintenanceRequestStatus.Pending,
            Priority = request.Priority ?? MaintenancePriority.Normal,
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

        // Whoever decides on it hears about it: the owner, and the managing broker if there is one.
        var deciders = await _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == lease.PropertyId)
            .Select(p => new
            {
                p.Title,
                p.OwnerUserId,
                BrokerUserId = _db.Brokers.Where(b => b.Id == p.BrokerId).Select(b => (Guid?)b.UserId).FirstOrDefault(),
                Category = _db.MaintenanceServiceTypes.Where(t => t.Id == request.ServiceTypeId)
                    .Select(t => new { t.NameAr, t.NameEn }).FirstOrDefault()
            })
            .FirstAsync(ct);

        var recipients = new List<Guid> { deciders.OwnerUserId };
        if (deciders.BrokerUserId is { } brokerUserId)
            recipients.Add(brokerUserId);

        await _notifications.NotifyAsync(recipients, MaintenanceNotifications.Created(new MaintenanceNotifications.Subject(
            maintenanceRequest.Id, deciders.Title, deciders.Category?.NameAr ?? string.Empty, deciders.Category?.NameEn ?? string.Empty)), ct);

        return Result<CreateMaintenanceRequestResponseDto>.Ok(new CreateMaintenanceRequestResponseDto
        {
            Id = maintenanceRequest.Id,
            RequestCode = maintenanceRequest.RequestNumber
        });
    }

    public static MaintenanceRatingDto ToRatingDto(MaintenanceRating rating)
    {
        int?[] criteria =
        [
            rating.TechnicianRating, rating.RepairQualityRating,
            rating.ResponseSpeedRating, rating.BrokerCommunicationRating
        ];
        var given = criteria.Where(c => c is not null).Select(c => c!.Value).ToList();

        return new MaintenanceRatingDto
        {
            TechnicianRating = rating.TechnicianRating,
            RepairQualityRating = rating.RepairQualityRating,
            ResponseSpeedRating = rating.ResponseSpeedRating,
            BrokerCommunicationRating = rating.BrokerCommunicationRating,
            Average = given.Count > 0 ? Math.Round((decimal)given.Sum() / given.Count, 1) : rating.Stars,
            Stars = rating.Stars,
            TagKeys = rating.TagKeys,
            Comment = rating.Comment,
            RatedAt = rating.UpdatedAt
        };
    }

    private sealed record ActionRow(
        MaintenanceActionType Type, Guid ActorUserId, string? ActorName, string? Message, DateTimeOffset At);

    // Whoever made the decision handles it. Before anyone has, it is the managing broker if the
    // property has one, otherwise the owner.
    private async Task<(MaintenanceHandledBy, string?)> ResolveHandlerAsync(
        List<ActionRow> actions, Guid ownerUserId, Guid? brokerId, CancellationToken ct)
    {
        var decision = actions.LastOrDefault(a =>
            a.Type is MaintenanceActionType.Accept or MaintenanceActionType.Reject or MaintenanceActionType.Delegate);

        var handledBy = decision switch
        {
            { Type: MaintenanceActionType.Delegate } => MaintenanceHandledBy.Broker,
            not null => decision.ActorUserId == ownerUserId ? MaintenanceHandledBy.Owner : MaintenanceHandledBy.Broker,
            null => brokerId is null ? MaintenanceHandledBy.Owner : MaintenanceHandledBy.Broker
        };

        // A broker that has since been unlinked from the property falls back to the owner's name
        // rather than showing nothing.
        if (handledBy == MaintenanceHandledBy.Broker && brokerId is not null)
        {
            var brokerName = await _db.Brokers
                .AsNoTracking()
                .Where(b => b.Id == brokerId)
                .Select(b => b.User.FullName)
                .FirstOrDefaultAsync(ct);

            return (handledBy, brokerName);
        }

        var ownerName = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == ownerUserId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);

        return (MaintenanceHandledBy.Owner, ownerName);
    }

    // One step per recorded action, oldest first. Inquiries and quotes are not steps on the
    // tenant's timeline: an inquiry leaves the request pending, so showing it would make the
    // progress look like it went backwards.
    private static List<MaintenanceStatusHistoryItemDto> BuildHistory(MaintenanceRequest request, List<ActionRow> actions)
    {
        var history = new List<MaintenanceStatusHistoryItemDto>
        {
            new() { Step = MaintenanceHistoryStep.Sent, At = request.CreatedAt }
        };

        foreach (var action in actions)
        {
            MaintenanceHistoryStep? step = action.Type switch
            {
                MaintenanceActionType.Accept => MaintenanceHistoryStep.Approved,
                MaintenanceActionType.Reject => MaintenanceHistoryStep.Rejected,
                MaintenanceActionType.Delegate => MaintenanceHistoryStep.Delegated,
                MaintenanceActionType.AssignTechnician => MaintenanceHistoryStep.TechnicianAssigned,
                _ => null
            };

            if (step is not null)
                history.Add(new()
                {
                    Step = step.Value, At = action.At,
                    ActorName = action.ActorName, Note = action.Message
                });
        }

        // Requests accepted before assignments were recorded: the technician came with the accept.
        var accept = actions.FirstOrDefault(a => a.Type == MaintenanceActionType.Accept);
        if (request.AssignedTechnicianId is not null && accept is not null &&
            !actions.Any(a => a.Type == MaintenanceActionType.AssignTechnician))
            history.Add(new() { Step = MaintenanceHistoryStep.TechnicianAssigned, At = accept.At, ActorName = accept.ActorName });

        if (request.CompletedAt is { } completedAt)
            history.Add(new() { Step = MaintenanceHistoryStep.Completed, At = completedAt });

        return history.OrderBy(h => h.At).ToList();
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
