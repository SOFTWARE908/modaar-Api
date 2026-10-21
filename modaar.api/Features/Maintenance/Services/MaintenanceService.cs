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
using modaar.api.Features.Users.Enums;
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

    public async Task<Result<MaintenanceListResultDto>> ListAsync(
        Guid callerUserId, MaintenanceListQueryDto query, AccountType activeRole, CancellationToken ct)
    {
        var caller = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == callerUserId && !u.IsDeleted && u.AccountTypes.Contains(activeRole))
            .Select(u => new { u.AccountTypes })
            .FirstOrDefaultAsync(ct);

        if (caller is null)
            return Result<MaintenanceListResultDto>.Fail(
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
        var ownersCount = await requests
            .Select(r => _db.Properties.Where(p => p.Id == r.PropertyId).Select(p => p.OwnerUserId).FirstOrDefault())
            .Distinct()
            .CountAsync(ct);

        var items = await MaintenanceListProjection.ToListItemsAsync(
            requests
                .OrderByDescending(m => m.CreatedAt)
                .Skip(query.SkipCount)
                .Take(query.MaxResultCount),
            _db, _language, callerUserId, ct);

        return Result<MaintenanceListResultDto>.Ok(
            new MaintenanceListResultDto { Items = items, TotalCount = totalCount, OwnersCount = ownersCount });
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
                    .FirstOrDefault(),
                Specialties = _db.TechnicianServiceTypes
                    .Where(s => s.TechnicianId == t.Id)
                    .Join(_db.MaintenanceServiceTypes, s => s.ServiceTypeId, st => st.Id,
                        (s, st) => new { st.NameAr, st.NameEn, st.SortOrder })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);

        // The owner as the broker sees them, and the owner's auto-approve limit for both sides.
        var owner = isBroker || isOwner
            ? await _db.Users.AsNoTracking()
                .Where(u => u.Id == property.OwnerUserId)
                .Select(u => new { u.Id, u.FullName, u.CountryCode, u.PhoneNumber, u.ProfileImageUrl, u.AutoApproveLimit })
                .FirstOrDefaultAsync(ct)
            : null;

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

        var facts = new MaintenanceFacts(
            request.Status, request.SubscriptionPlan, property.BrokerId is not null, request.QuoteRequired,
            request.QuotedAt is not null, request.QuoteApprovedAt is not null, technician is not null, rating is not null);

        var (handledBy, handledByName) = await ResolveHandlerAsync(
            facts.BrokerHandles, property.OwnerUserId, property.BrokerId, ct);

        var visitDate = request.ScheduledVisitDate ?? request.PreferredVisitDate;
        var visitSlot = request.ScheduledTimeSlot ?? request.PreferredTimeSlot;
        var today = SaudiTime.Today(_time);

        var categoryName = serviceType is null ? string.Empty : _language.Pick(serviceType.NameAr, serviceType.NameEn);
        var stage = MaintenanceStages.Derive(facts);

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
            NextActionLabel = MaintenanceStages.NextActionLabel(stage, visitDate, rating is not null, _language),
            RejectedBy = request.RejectedBy,
            RejectionReason = request.RejectionReason,
            HandledBy = handledBy,
            HandledByName = handledByName,
            SubscriptionPlan = facts.EffectivePlan,
            AvailableActions = MaintenanceStages.Available(facts, new MaintenanceCaller(isTenant, isOwner, isBroker)),
            StatusHistory = BuildHistory(request, actions),
            VisitDate = visitDate,
            VisitDayIsTomorrow = visitDate is { } d && d.DayNumber == today.DayNumber + 1,
            VisitTimeRange = visitSlot is { } slot ? VisitSlots.Format(slot) : null,
            VisitTimePeriod = visitSlot,
            VisitTime = request.ScheduledVisitTime,
            AssignedTechnician = technician is null ? null : new TechnicianDto
            {
                Id = technician.Id,
                Name = technician.User?.FullName ?? string.Empty,
                Role = _language.Pick(technician.RoleAr, technician.RoleEn),
                ImageUrl = technician.User?.ProfileImageUrl,
                AverageRating = technician.RatingAverage,
                PhoneNumber = technician.User is null ? null : technician.User.CountryCode + technician.User.PhoneNumber,
                Specialty = MaintenanceListProjection.Specialty(
                    technician.Specialties.Select(s => (s.NameAr, s.NameEn, s.SortOrder)), _language)
            },
            Quote = request.QuotedAt is null ? null : new MaintenanceQuoteDto
            {
                PartsCost = request.QuotePartsCost ?? 0,
                LaborCost = request.QuoteLaborCost ?? 0,
                Total = (request.QuotePartsCost ?? 0) + (request.QuoteLaborCost ?? 0),
                CostBearer = request.QuoteCostBearer ?? MaintenanceCostBearer.Owner,
                Description = request.QuoteDescription,
                QuotedAt = request.QuotedAt,
                ApprovedAt = request.QuoteApprovedAt,
                AutoApproved = request.QuoteAutoApproved
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
            Tenant = tenant,
            Owner = isBroker && owner is not null ? new MaintenanceOwnerDto
            {
                Id = owner.Id,
                Name = owner.FullName,
                PhoneNumber = owner.CountryCode + owner.PhoneNumber,
                AvatarUrl = owner.ProfileImageUrl
            } : null,
            AutoApproveLimit = facts.NeedsOwnerApproval ? owner?.AutoApproveLimit : null
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

        // The owner's plan at this moment decides the workflow for the life of the request. With no
        // broker on the property there is no one else to run it, whatever the plan says.
        var handling = await _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == lease.PropertyId)
            .Select(p => new
            {
                HasBroker = p.BrokerId != null,
                Plan = _db.Users.Where(u => u.Id == p.OwnerUserId).Select(u => u.SubscriptionPlan).FirstOrDefault()
            })
            .FirstAsync(ct);

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
            SubscriptionPlan = handling.HasBroker ? handling.Plan : OwnerSubscriptionPlan.SelfManaged,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.MaintenanceRequests.Add(maintenanceRequest);

        foreach (var attachment in attachments)
            attachment.RequestId = maintenanceRequest.Id;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Maintenance request {Number} created by {UserId} on property {PropertyId}.",
            maintenanceRequest.RequestNumber, tenantUserId, lease.PropertyId);

        // Whoever decides on it hears about it: the owner on SelfManaged, the managing broker otherwise.
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

        var recipients = maintenanceRequest.SubscriptionPlan != OwnerSubscriptionPlan.SelfManaged &&
                         deciders.BrokerUserId is { } brokerUserId
            ? new List<Guid> { brokerUserId }
            : new List<Guid> { deciders.OwnerUserId };

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

    // The owner's plan decides who runs the request: the owner on SelfManaged (or when the
    // property has no broker), the managing broker otherwise.
    private async Task<(MaintenanceHandledBy, string?)> ResolveHandlerAsync(
        bool brokerHandles, Guid ownerUserId, Guid? brokerId, CancellationToken ct)
    {
        var handledBy = brokerHandles ? MaintenanceHandledBy.Broker : MaintenanceHandledBy.Owner;

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

    // One step per recorded action, oldest first. Inquiries are not steps on the
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
                MaintenanceActionType.Quote => MaintenanceHistoryStep.QuoteSubmitted,
                MaintenanceActionType.ApproveQuote => MaintenanceHistoryStep.OwnerApproved,
                // A rejected quote closes the request, so it reads as a rejection.
                MaintenanceActionType.RejectQuote => MaintenanceHistoryStep.Rejected,
                MaintenanceActionType.Reschedule => MaintenanceHistoryStep.Rescheduled,
                MaintenanceActionType.Complete => MaintenanceHistoryStep.Completed,
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

        // Requests completed before completion was an action.
        if (request.CompletedAt is { } completedAt && !actions.Any(a => a.Type == MaintenanceActionType.Complete))
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
