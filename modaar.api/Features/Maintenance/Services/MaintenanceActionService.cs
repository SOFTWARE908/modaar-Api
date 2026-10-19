using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Notifications.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

public interface IMaintenanceActionService
{
    Task<Result<bool>> AcceptAsync(Guid requestId, Guid callerUserId, AcceptMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> RejectAsync(Guid requestId, Guid callerUserId, RejectMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> InquiryAsync(Guid requestId, Guid callerUserId, MaintenanceInquiryDto request, CancellationToken ct);
    Task<Result<bool>> DelegateAsync(Guid requestId, Guid callerUserId, DelegateMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> SubmitQuoteAsync(Guid requestId, Guid callerUserId, SubmitMaintenanceQuoteDto request, CancellationToken ct);
}

public sealed class MaintenanceActionService : IMaintenanceActionService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly INotificationService _notifications;
    private readonly ILogger<MaintenanceActionService> _logger;

    public MaintenanceActionService(
        ModaarDbContext db, TimeProvider time, INotificationService notifications, ILogger<MaintenanceActionService> logger)
    {
        _db = db;
        _time = time;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<Result<bool>> AcceptAsync(
        Guid requestId, Guid callerUserId, AcceptMaintenanceRequestDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, ct);
        if (ctx.Failure is { } failure) return failure;

        if (!ctx.IsOwner && !ctx.IsManagingBroker)
            return Forbidden("Only the property owner or its managing broker can accept a request.");

        // A technician must belong to the accepting brokerage, or be independent. Otherwise an
        // owner could assign someone else's staff to their job.
        if (request.TechnicianId is { } technicianId)
        {
            var technician = await _db.Technicians
                .AsNoTracking()
                .Where(t => t.Id == technicianId)
                .Select(t => new { t.BrokerId, t.IsAvailable })
                .FirstOrDefaultAsync(ct);

            if (technician is null)
                return Result<bool>.Fail(AppErrorCode.NotFound, "Technician not found.");

            if (!technician.IsAvailable)
                return Result<bool>.Fail(AppErrorCode.Conflict, "That technician is not available.");

            if (technician.BrokerId is not null && technician.BrokerId != ctx.Property.BrokerId)
                return Forbidden("That technician belongs to a different brokerage.");

            ctx.Request.AssignedTechnicianId = technicianId;

            // Its own audit row so the tenant's status history can date the assignment.
            var technicianName = await _db.Technicians
                .Where(t => t.Id == technicianId)
                .Select(t => _db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault())
                .FirstOrDefaultAsync(ct);

            _db.MaintenanceRequestActions.Add(new MaintenanceRequestAction
            {
                Id = Guid.NewGuid(),
                RequestId = ctx.Request.Id,
                ActorUserId = callerUserId,
                Type = MaintenanceActionType.AssignTechnician,
                Message = technicianName,
                CreatedAt = _time.GetUtcNow()
            });
        }

        ctx.Request.QuoteRequired = request.RequiresQuote;

        if (request.ScheduledVisitDate is { } date && request.ScheduledTimeSlot is { } slot)
        {
            // Scheduling a visit in the past is a data-entry mistake, not a valid plan.
            if (date < SaudiTime.Today(_time))
                return Result<bool>.Fail(AppErrorCode.ValidationFailed, "The visit date is in the past.");

            ctx.Request.ScheduledVisitDate = date;
            ctx.Request.ScheduledTimeSlot = (MaintenanceTimeSlot)slot;
        }
        else
        {
            // No counter-offer: what the tenant asked for becomes what was agreed. If they asked
            // for nothing, the visit stays unscheduled until someone sets one.
            ctx.Request.ScheduledVisitDate = ctx.Request.PreferredVisitDate;
            ctx.Request.ScheduledTimeSlot = ctx.Request.PreferredTimeSlot;
        }
        // إعادة الترتيب أثناء الفرز: المستأجر يرفع ما يراه، والمالك يعرف ما يحتمل التأجيل.
        if (request.Priority is { } priority)
            ctx.Request.Priority = (MaintenancePriority)priority;

        return await TransitionAsync(ctx, MaintenanceRequestStatus.Accepted, MaintenanceActionType.Accept, null, null, ct);
    }

    public async Task<Result<bool>> RejectAsync(
        Guid requestId, Guid callerUserId, RejectMaintenanceRequestDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, ct);
        if (ctx.Failure is { } failure) return failure;

        if (!ctx.IsOwner && !ctx.IsManagingBroker)
            return Forbidden("Only the property owner or its managing broker can reject a request.");

        ctx.Request.RejectedBy = ctx.IsOwner
            ? MaintenanceRequestRejectedBy.Owner
            : MaintenanceRequestRejectedBy.Broker;

        ctx.Request.RejectionReason = request.Reason.Trim();

        return await TransitionAsync(ctx, MaintenanceRequestStatus.Rejected, MaintenanceActionType.Reject,
            request.Reason.Trim(), null, ct);
    }

    public async Task<Result<bool>> InquiryAsync(
        Guid requestId, Guid callerUserId, MaintenanceInquiryDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, ct);
        if (ctx.Failure is { } failure) return failure;

        if (!ctx.IsOwner && !ctx.IsManagingBroker)
            return Forbidden("Only the property owner or its managing broker can raise an inquiry.");

        // An inquiry asks the tenant a question; it does not decide anything, so the request
        // stays Pending and can still be accepted or rejected afterwards.
        return await TransitionAsync(ctx, null, MaintenanceActionType.Inquiry,
            request.Message.Trim(), null, ct);
    }

    public async Task<Result<bool>> DelegateAsync(
        Guid requestId, Guid callerUserId, DelegateMaintenanceRequestDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, ct);
        if (ctx.Failure is { } failure) return failure;

        // Owner only. A broker cannot hand a job on to someone else.
        if (!ctx.IsOwner)
            return Forbidden("Only the property owner can delegate a request.");

        var brokerId = request.BrokerId ?? ctx.Property.BrokerId;

        if (brokerId is null)
            return Result<bool>.Fail(AppErrorCode.Conflict,
                "This property has no managing broker to delegate to.");

        // Delegating to an unrelated brokerage would grant them access to a property they have no
        // relationship with, so the target must be the one already managing it.
        if (brokerId != ctx.Property.BrokerId)
            return Forbidden("That broker does not manage this property.");

        ctx.Request.AssignedTechnicianId = null;

        return await TransitionAsync(ctx, MaintenanceRequestStatus.Accepted, MaintenanceActionType.Delegate,
            null, brokerId, ct);
    }

    public async Task<Result<bool>> SubmitQuoteAsync(
        Guid requestId, Guid callerUserId, SubmitMaintenanceQuoteDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, ct, requirePending: false);
        if (ctx.Failure is { } failure) return failure;

        if (!ctx.IsOwner && !ctx.IsManagingBroker)
            return Forbidden("Only the property owner or its managing broker can submit a quote.");

        // Pricing happens between accepting and finishing the work. A quote on a pending request
        // skips the decision; one on a closed request changes nothing anyone will act on.
        if (ctx.Request.Status is not (MaintenanceRequestStatus.Accepted
                                    or MaintenanceRequestStatus.OnTheWay
                                    or MaintenanceRequestStatus.InProgress))
            return Result<bool>.Fail(AppErrorCode.Conflict,
                $"This request is {ctx.Request.Status}; a quote can only be added once it is accepted and before it is completed.");

        ctx.Request.QuotePartsCost = request.PartsCost;
        ctx.Request.QuoteLaborCost = request.LaborCost;
        ctx.Request.QuoteCostBearer = (MaintenanceCostBearer)request.CostBearer;
        ctx.Request.QuoteDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ctx.Request.QuotedAt = _time.GetUtcNow();

        return await TransitionAsync(ctx, null, MaintenanceActionType.Quote,
            $"{request.PartsCost + request.LaborCost:0.##} ({request.CostBearer})", null, ct);
    }

    // Loads the request and works out who the caller is to it. Every action starts here, so the
    // pending-only rule and the not-found/forbidden shapes are decided in one place.
    private async Task<ActionContext> ResolveAsync(
        Guid requestId, Guid callerUserId, CancellationToken ct, bool requirePending = true)
    {
        var request = await _db.MaintenanceRequests.FirstOrDefaultAsync(m => m.Id == requestId, ct);
        if (request is null)
            return ActionContext.Failed(Result<bool>.Fail(AppErrorCode.NotFound, "Request not found."));

        var property = await _db.Properties
            .AsNoTracking()
            .Where(p => p.Id == request.PropertyId)
            .Select(p => new PropertyRef(p.Id, p.OwnerUserId, p.BrokerId))
            .FirstOrDefaultAsync(ct);

        if (property is null)
            return ActionContext.Failed(Result<bool>.Fail(AppErrorCode.NotFound, "Property not found."));

        // Actions are only available while the request is awaiting a decision. Without this, a
        // completed job could be rejected months later.
        if (requirePending && request.Status != MaintenanceRequestStatus.Pending)
            return ActionContext.Failed(Result<bool>.Fail(AppErrorCode.Conflict,
                $"This request is {request.Status} and can no longer be actioned."));

        var isOwner = property.OwnerUserId == callerUserId;

        var isManagingBroker = property.BrokerId is { } brokerId &&
            await _db.Brokers.AnyAsync(b => b.Id == brokerId && b.UserId == callerUserId, ct);

        return new ActionContext
        {
            Request = request,
            Property = property,
            CallerUserId = callerUserId,
            IsOwner = isOwner,
            IsManagingBroker = isManagingBroker
        };
    }

    // One write path for every action: the status change and the audit row always land together.
    private async Task<Result<bool>> TransitionAsync(
        ActionContext ctx,
        MaintenanceRequestStatus? newStatus,
        MaintenanceActionType actionType,
        string? message,
        Guid? delegatedToBrokerId,
        CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        if (newStatus is { } status)
            ctx.Request.Status = status;

        ctx.Request.UpdatedAt = now;

        _db.MaintenanceRequestActions.Add(new MaintenanceRequestAction
        {
            Id = Guid.NewGuid(),
            RequestId = ctx.Request.Id,
            ActorUserId = ctx.CallerUserId,
            Type = actionType,
            Message = message,
            DelegatedToBrokerId = delegatedToBrokerId,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Maintenance {Number}: {Action} by {UserId}; status now {Status}.",
            ctx.Request.RequestNumber, actionType, ctx.CallerUserId, ctx.Request.Status);

        // After the save, never before: a notification about an action that then failed to save
        // would announce something that didn't happen.
        await NotifyAsync(ctx, actionType, message, delegatedToBrokerId, ct);

        return Result<bool>.Ok(true);
    }

    // Every action's notification is decided here, because every action passes through
    // TransitionAsync. Who gets what:
    //   accept / reject / inquiry / quote → the tenant
    //   delegate                          → the tenant, and the broker it was handed to
    private async Task NotifyAsync(
        ActionContext ctx, MaintenanceActionType actionType, string? message, Guid? delegatedToBrokerId, CancellationToken ct)
    {
        var subject = await LoadSubjectAsync(ctx.Request, ct);
        var tenant = new[] { ctx.Request.TenantUserId };

        switch (actionType)
        {
            case MaintenanceActionType.Accept:
                var technicianName = ctx.Request.AssignedTechnicianId is { } technicianId
                    ? await _db.Technicians
                        .Where(t => t.Id == technicianId)
                        .Select(t => _db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault())
                        .FirstOrDefaultAsync(ct)
                    : null;
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.Accepted(subject, technicianName), ct);
                break;

            case MaintenanceActionType.Reject:
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.Rejected(subject, message), ct);
                break;

            case MaintenanceActionType.Inquiry:
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.Inquiry(subject, message), ct);
                break;

            case MaintenanceActionType.Quote:
                var total = (ctx.Request.QuotePartsCost ?? 0) + (ctx.Request.QuoteLaborCost ?? 0);
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.Quoted(subject, total), ct);
                break;

            case MaintenanceActionType.Delegate:
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.DelegatedToTenant(subject), ct);

                var brokerUserId = await _db.Brokers
                    .Where(b => b.Id == delegatedToBrokerId)
                    .Select(b => (Guid?)b.UserId)
                    .FirstOrDefaultAsync(ct);
                if (brokerUserId is { } brokerUser)
                    await _notifications.NotifyAsync([brokerUser], MaintenanceNotifications.DelegatedToBroker(subject), ct);
                break;
        }
    }

    private async Task<MaintenanceNotifications.Subject> LoadSubjectAsync(MaintenanceRequest request, CancellationToken ct)
    {
        var row = await _db.MaintenanceRequests
            .AsNoTracking()
            .Where(m => m.Id == request.Id)
            .Select(m => new
            {
                PropertyTitle = _db.Properties.Where(p => p.Id == m.PropertyId).Select(p => p.Title).FirstOrDefault(),
                Category = _db.MaintenanceServiceTypes.Where(t => t.Id == m.ServiceTypeId)
                    .Select(t => new { t.NameAr, t.NameEn }).FirstOrDefault()
            })
            .FirstAsync(ct);

        return new MaintenanceNotifications.Subject(
            request.Id, row.PropertyTitle ?? string.Empty, row.Category?.NameAr ?? string.Empty, row.Category?.NameEn ?? string.Empty);
    }

    private static Result<bool> Forbidden(string message) =>
        Result<bool>.Fail(AppErrorCode.Forbidden, message);

    private sealed record PropertyRef(Guid Id, Guid OwnerUserId, Guid? BrokerId);

    private sealed class ActionContext
    {
        public MaintenanceRequest Request { get; init; } = null!;
        public PropertyRef Property { get; init; }
        public Guid CallerUserId { get; init; }
        public bool IsOwner { get; init; }
        public bool IsManagingBroker { get; init; }

        public Result<bool>? Failure { get; private init; }

        public static ActionContext Failed(Result<bool> failure) => new() { Failure = failure };
    }
}
