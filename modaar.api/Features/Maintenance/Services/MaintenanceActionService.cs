using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

public interface IMaintenanceActionService
{
    Task<Result<bool>> AcceptAsync(Guid requestId, Guid callerUserId, AcceptMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> RejectAsync(Guid requestId, Guid callerUserId, RejectMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> InquiryAsync(Guid requestId, Guid callerUserId, MaintenanceInquiryDto request, CancellationToken ct);
    Task<Result<bool>> DelegateAsync(Guid requestId, Guid callerUserId, DelegateMaintenanceRequestDto request, CancellationToken ct);
}

public sealed class MaintenanceActionService : IMaintenanceActionService
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<MaintenanceActionService> _logger;

    public MaintenanceActionService(ModaarDbContext db, TimeProvider time, ILogger<MaintenanceActionService> logger)
    {
        _db = db;
        _time = time;
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
        }

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
            // No counter-offer: what the tenant asked for becomes what was agreed.
            ctx.Request.ScheduledVisitDate = ctx.Request.PreferredVisitDate;
            ctx.Request.ScheduledTimeSlot = ctx.Request.PreferredTimeSlot;
        }

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

    // Loads the request and works out who the caller is to it. Every action starts here, so the
    // pending-only rule and the not-found/forbidden shapes are decided in one place.
    private async Task<ActionContext> ResolveAsync(Guid requestId, Guid callerUserId, CancellationToken ct)
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
        if (request.Status != MaintenanceRequestStatus.Pending)
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

        return Result<bool>.Ok(true);
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
