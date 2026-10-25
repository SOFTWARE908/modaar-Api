using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Common.Time;
using modaar.api.Features.Maintenance.Dtos;
using modaar.api.Features.Maintenance.Entities;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Notifications.Services;
using modaar.api.Features.Users.Enums;
using modaar.api.Features.Users.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Maintenance.Services;

public interface IMaintenanceActionService
{
    Task<Result<bool>> AcceptAsync(Guid requestId, Guid callerUserId, AcceptMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> RejectAsync(Guid requestId, Guid callerUserId, RejectMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> InquiryAsync(Guid requestId, Guid callerUserId, MaintenanceInquiryDto request, CancellationToken ct);
    Task<Result<bool>> DelegateAsync(Guid requestId, Guid callerUserId, DelegateMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> SubmitQuoteAsync(Guid requestId, Guid callerUserId, SubmitMaintenanceQuoteDto request, CancellationToken ct);
    Task<Result<bool>> ApproveQuoteAsync(Guid requestId, Guid callerUserId, CancellationToken ct);
    Task<Result<bool>> RejectQuoteAsync(Guid requestId, Guid callerUserId, RejectMaintenanceRequestDto request, CancellationToken ct);
    Task<Result<bool>> AssignTechnicianAsync(Guid requestId, Guid callerUserId, AssignTechnicianDto request, CancellationToken ct);
    Task<Result<bool>> RescheduleAsync(Guid requestId, Guid callerUserId, RescheduleMaintenanceVisitDto request, CancellationToken ct);
    Task<Result<bool>> CompleteAsync(Guid requestId, Guid callerUserId, CancellationToken ct);
}

// Every owner/broker action on a request. Which action is allowed, for whom, at which point, is
// not decided here: MaintenanceStages.Available is the single rule, and each action starts by
// checking it. That is what keeps the buttons the app shows and what the server accepts in step.
//
// The three workflows, by the owner's subscription plan (snapshotted on the request):
//   SelfManaged  tenant → owner accepts → (quote, technician optional) → owner completes
//   Managed      broker accepts → broker quotes → owner approves (or auto under the limit)
//                → broker assigns technician + visit time → broker completes
//   FullyManaged broker accepts → (quote optional, broker pays) → assigns → completes
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
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.Accept, ct);
        if (ctx.Failure is { } failure) return failure;

        // Managed always prices first: the owner has to see the cost before anyone is sent.
        ctx.Request.QuoteRequired = ctx.Facts.NeedsOwnerApproval || request.RequiresQuote;

        // A technician named on accept skips the pricing step, so it only fits when there is none.
        if (request.TechnicianId is { } technicianId)
        {
            if (ctx.Request.QuoteRequired)
                return Result<bool>.Fail(AppErrorCode.Conflict,
                    "This request is priced first; assign the technician once the quote is settled.");

            var technician = await LoadTechnicianAsync(technicianId, ctx, ct);
            if (technician.Failure is { } techFailure) return techFailure;

            ctx.Request.AssignedTechnicianId = technicianId;

            // Its own audit row so the tenant's status history can date the assignment.
            AddAction(ctx, MaintenanceActionType.AssignTechnician, technician.Name);
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
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.Reject, ct);
        if (ctx.Failure is { } failure) return failure;

        ctx.Request.RejectedBy = ctx.Facts.BrokerHandles
            ? MaintenanceRequestRejectedBy.Broker
            : MaintenanceRequestRejectedBy.Owner;

        ctx.Request.RejectionReason = request.Reason.Trim();

        return await TransitionAsync(ctx, MaintenanceRequestStatus.Rejected, MaintenanceActionType.Reject,
            request.Reason.Trim(), null, ct);
    }

    public async Task<Result<bool>> InquiryAsync(
        Guid requestId, Guid callerUserId, MaintenanceInquiryDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.Inquiry, ct);
        if (ctx.Failure is { } failure) return failure;

        // An inquiry asks a question; it decides nothing, so the request stays where it is.
        // While pending it goes to the tenant; while the quote waits it goes to the broker.
        return await TransitionAsync(ctx, null, MaintenanceActionType.Inquiry,
            request.Message.Trim(), null, ct);
    }

    public async Task<Result<bool>> DelegateAsync(
        Guid requestId, Guid callerUserId, DelegateMaintenanceRequestDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.Delegate, ct);
        if (ctx.Failure is { } failure) return failure;

        var brokerId = request.BrokerId ?? ctx.Property.BrokerId;

        // Delegating to an unrelated brokerage would grant them access to a property they have no
        // relationship with, so the target must be the one already managing it.
        if (brokerId != ctx.Property.BrokerId)
            return Forbidden("That broker does not manage this property.");

        // A SelfManaged owner handing one job over: from here it runs as Managed — the broker
        // prices it and the owner still approves the price.
        ctx.Request.SubscriptionPlan = OwnerSubscriptionPlan.Managed;
        ctx.Request.QuoteRequired = true;
        ctx.Request.AssignedTechnicianId = null;

        return await TransitionAsync(ctx, MaintenanceRequestStatus.Accepted, MaintenanceActionType.Delegate,
            null, brokerId, ct);
    }

    public async Task<Result<bool>> SubmitQuoteAsync(
        Guid requestId, Guid callerUserId, SubmitMaintenanceQuoteDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.SubmitQuote, ct);
        if (ctx.Failure is { } failure) return failure;

        var costBearer = (MaintenanceCostBearer)request.CostBearer;

        // Only the full package puts the cost on the broker.
        if (costBearer == MaintenanceCostBearer.Broker &&
            ctx.Facts.EffectivePlan != OwnerSubscriptionPlan.FullyManaged)
            return Result<bool>.Fail(AppErrorCode.ValidationFailed,
                "The broker carries the cost only on a fully managed subscription.");

        var now = _time.GetUtcNow();

        ctx.Request.QuotePartsCost = request.PartsCost;
        ctx.Request.QuoteLaborCost = request.LaborCost;
        ctx.Request.QuoteCostBearer = costBearer;
        ctx.Request.QuoteDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ctx.Request.QuotedAt = now;

        // A new price needs a new answer.
        ctx.Request.QuoteApprovedAt = null;
        ctx.Request.QuoteApprovedByUserId = null;
        ctx.Request.QuoteAutoApproved = false;

        var total = request.PartsCost + request.LaborCost;

        if (ctx.Facts.NeedsOwnerApproval)
        {
            // Read live, not snapshotted: the limit is the owner's standing instruction to the broker
            // for this building.
            var limit = (await BuildingSubscriptions.ForPropertyAsync(_db, ctx.Property.Id, ct)).AutoApproveLimit;

            if (limit is { } max && total <= max)
            {
                ctx.Request.QuoteApprovedAt = now;
                ctx.Request.QuoteAutoApproved = true;
                ctx.AutoApproved = true;
            }
        }

        return await TransitionAsync(ctx, null, MaintenanceActionType.Quote,
            $"{total:0.##} ({costBearer})", null, ct);
    }

    public async Task<Result<bool>> ApproveQuoteAsync(Guid requestId, Guid callerUserId, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.ApproveQuote, ct);
        if (ctx.Failure is { } failure) return failure;

        ctx.Request.QuoteApprovedAt = _time.GetUtcNow();
        ctx.Request.QuoteApprovedByUserId = callerUserId;
        ctx.Request.QuoteAutoApproved = false;

        return await TransitionAsync(ctx, null, MaintenanceActionType.ApproveQuote, null, null, ct);
    }

    public async Task<Result<bool>> RejectQuoteAsync(
        Guid requestId, Guid callerUserId, RejectMaintenanceRequestDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.RejectQuote, ct);
        if (ctx.Failure is { } failure) return failure;

        // The owner turning down the price closes the request. To negotiate instead they use an
        // inquiry, and the broker can revise the quote.
        ctx.Request.RejectedBy = MaintenanceRequestRejectedBy.Owner;
        ctx.Request.RejectionReason = request.Reason.Trim();

        return await TransitionAsync(ctx, MaintenanceRequestStatus.Rejected, MaintenanceActionType.RejectQuote,
            request.Reason.Trim(), null, ct);
    }

    public async Task<Result<bool>> AssignTechnicianAsync(
        Guid requestId, Guid callerUserId, AssignTechnicianDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.AssignTechnician, ct);
        if (ctx.Failure is { } failure) return failure;

        if (VisitInPast(request.VisitDate, request.VisitTime) is { } pastFailure) return pastFailure;

        var technician = await LoadTechnicianAsync(request.TechnicianId, ctx, ct);
        if (technician.Failure is { } techFailure) return techFailure;

        if (await BusyAsync(request.TechnicianId, request.VisitDate, request.VisitTime, ctx.Request.Id, ct) is { } busy)
            return busy;

        ctx.Request.AssignedTechnicianId = request.TechnicianId;
        SetVisit(ctx.Request, request.VisitDate, request.VisitTime);

        return await TransitionAsync(ctx, null, MaintenanceActionType.AssignTechnician, technician.Name, null, ct);
    }

    public async Task<Result<bool>> RescheduleAsync(
        Guid requestId, Guid callerUserId, RescheduleMaintenanceVisitDto request, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.Reschedule, ct);
        if (ctx.Failure is { } failure) return failure;

        if (VisitInPast(request.VisitDate, request.VisitTime) is { } pastFailure) return pastFailure;

        if (ctx.Request.AssignedTechnicianId is { } technicianId
            && await BusyAsync(technicianId, request.VisitDate, request.VisitTime, ctx.Request.Id, ct) is { } busy)
            return busy;

        SetVisit(ctx.Request, request.VisitDate, request.VisitTime);

        return await TransitionAsync(ctx, null, MaintenanceActionType.Reschedule,
            $"{request.VisitDate:yyyy-MM-dd} {request.VisitTime:HH\\:mm}", null, ct);
    }

    public async Task<Result<bool>> CompleteAsync(Guid requestId, Guid callerUserId, CancellationToken ct)
    {
        var ctx = await ResolveAsync(requestId, callerUserId, MaintenanceAvailableAction.Complete, ct);
        if (ctx.Failure is { } failure) return failure;

        // Completing is what opens rating for the tenant.
        ctx.Request.CompletedAt = _time.GetUtcNow();

        return await TransitionAsync(ctx, MaintenanceRequestStatus.Completed, MaintenanceActionType.Complete, null, null, ct);
    }

    // Loads the request, works out who the caller is to it, and checks the action against
    // MaintenanceStages.Available. Every action starts here.
    private async Task<ActionContext> ResolveAsync(
        Guid requestId, Guid callerUserId, MaintenanceAvailableAction action, CancellationToken ct)
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

        var isOwner = property.OwnerUserId == callerUserId;

        var isManagingBroker = property.BrokerId is { } brokerId &&
            await _db.Brokers.AnyAsync(b => b.Id == brokerId && b.UserId == callerUserId, ct);

        var isRated = await _db.MaintenanceRatings.AnyAsync(r => r.RequestId == requestId, ct);

        var facts = new MaintenanceFacts(
            request.Status, request.SubscriptionPlan, property.BrokerId is not null, request.QuoteRequired,
            request.QuotedAt is not null, request.QuoteApprovedAt is not null,
            request.AssignedTechnicianId is not null, isRated);

        var caller = new MaintenanceCaller(request.TenantUserId == callerUserId, isOwner, isManagingBroker);

        if (!MaintenanceStages.Available(facts, caller).Contains(action))
        {
            // Someone could do this right now, just not this caller → 403. Nobody could → 409,
            // because the request is past (or not yet at) the point where it applies.
            var anyone = new MaintenanceCaller(true, true, true);
            var stage = MaintenanceStages.Derive(facts);

            return ActionContext.Failed(MaintenanceStages.Available(facts, anyone).Contains(action)
                ? Forbidden($"You cannot {action} this request: on a {facts.EffectivePlan} subscription that is up to someone else.")
                : Result<bool>.Fail(AppErrorCode.Conflict, $"This request is at stage {stage}; {action} is not possible now."));
        }

        return new ActionContext
        {
            Request = request,
            Property = property,
            CallerUserId = callerUserId,
            Facts = facts,
            StageBefore = MaintenanceStages.Derive(facts)
        };
    }

    // A technician must belong to the property's brokerage, or be independent. Otherwise one
    // brokerage could send another's staff to a job.
    private async Task<TechnicianRef> LoadTechnicianAsync(Guid technicianId, ActionContext ctx, CancellationToken ct)
    {
        var technician = await _db.Technicians
            .AsNoTracking()
            .Where(t => t.Id == technicianId)
            .Select(t => new
            {
                t.BrokerId,
                t.IsAvailable,
                t.UserId,
                Name = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (technician is null)
            return TechnicianRef.Failed(Result<bool>.Fail(AppErrorCode.NotFound, "Technician not found."));

        if (!technician.IsAvailable)
            return TechnicianRef.Failed(Result<bool>.Fail(AppErrorCode.Conflict, "That technician is not available."));

        if (technician.BrokerId is not null && technician.BrokerId != ctx.Property.BrokerId)
            return TechnicianRef.Failed(Forbidden("That technician belongs to a different brokerage."));

        return new TechnicianRef { Name = technician.Name, UserId = technician.UserId };
    }

    // 409 when the technician is booked then, or it's outside their working hours (if set).
    // This request's own current visit doesn't count, so rescheduling into an overlapping time works.
    private async Task<Result<bool>?> BusyAsync(Guid technicianId, DateOnly date, TimeOnly time, Guid requestId, CancellationToken ct)
    {
        var calendar = (await TechnicianSchedule.LoadAsync(_db, [technicianId], date, ct))[technicianId];
        return calendar.WhyNot(date.ToDateTime(time), requestId) is { } reason
            ? Result<bool>.Fail(AppErrorCode.Conflict, reason)
            : null;
    }

    private Result<bool>? VisitInPast(DateOnly date, TimeOnly time)
    {
        // Visit times are Riyadh wall-clock times.
        var now = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), SaudiTime.Zone).DateTime;
        return date.ToDateTime(time) < now
            ? Result<bool>.Fail(AppErrorCode.ValidationFailed, "The visit time is in the past.")
            : null;
    }

    // The exact time is what was agreed; the slot is kept in step so older screens that only read
    // the slot still show the right part of the day.
    private static void SetVisit(MaintenanceRequest request, DateOnly date, TimeOnly time)
    {
        request.ScheduledVisitDate = date;
        request.ScheduledVisitTime = time;
        request.ScheduledTimeSlot = time < VisitSlots.Range(MaintenanceTimeSlot.Afternoon).Start
            ? MaintenanceTimeSlot.Morning
            : time < VisitSlots.Range(MaintenanceTimeSlot.Evening).Start
                ? MaintenanceTimeSlot.Afternoon
                : MaintenanceTimeSlot.Evening;
    }

    private void AddAction(ActionContext ctx, MaintenanceActionType type, string? message, Guid? actorUserId = null) =>
        _db.MaintenanceRequestActions.Add(new MaintenanceRequestAction
        {
            Id = Guid.NewGuid(),
            RequestId = ctx.Request.Id,
            ActorUserId = actorUserId ?? ctx.CallerUserId,
            Type = type,
            Message = message,
            CreatedAt = _time.GetUtcNow()
        });

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

        // Under the owner's limit: the approval is the owner's standing rule, so it is recorded
        // in their name, a moment after the quote so the history reads in order.
        if (ctx.AutoApproved)
            _db.MaintenanceRequestActions.Add(new MaintenanceRequestAction
            {
                Id = Guid.NewGuid(),
                RequestId = ctx.Request.Id,
                ActorUserId = ctx.Property.OwnerUserId,
                Type = MaintenanceActionType.ApproveQuote,
                Message = "auto",
                CreatedAt = now.AddMilliseconds(1)
            });

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Maintenance {Number}: {Action} by {UserId}; status now {Status}.",
            ctx.Request.RequestNumber, actionType, ctx.CallerUserId, ctx.Request.Status);

        // After the save, never before: a notification about an action that then failed to save
        // would announce something that didn't happen.
        await NotifyAsync(ctx, actionType, message, delegatedToBrokerId, ct);

        return Result<bool>.Ok(true);
    }

    // Every action's notification is decided here. Who gets what:
    //   accept / reject                  → tenant
    //   inquiry                          → tenant while pending; broker when the owner asks about a quote
    //   delegate                         → tenant, and the broker it was handed to
    //   quote                            → tenant; owner too when it needs their approval
    //   approve quote                    → broker
    //   reject quote                     → broker and tenant
    //   assign technician / reschedule   → tenant and technician
    //   complete                         → tenant; owner too when the broker ran it
    private async Task NotifyAsync(
        ActionContext ctx, MaintenanceActionType actionType, string? message, Guid? delegatedToBrokerId, CancellationToken ct)
    {
        var subject = await LoadSubjectAsync(ctx.Request, ct);
        Guid[] tenant = [ctx.Request.TenantUserId];
        Guid[] owner = [ctx.Property.OwnerUserId];

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

            case MaintenanceActionType.Inquiry when ctx.StageBefore == MaintenanceStage.AwaitingOwnerApproval:
                await _notifications.NotifyAsync(await BrokerAsync(ctx, ct), MaintenanceNotifications.OwnerInquiry(subject, message), ct);
                break;

            case MaintenanceActionType.Inquiry:
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.Inquiry(subject, message), ct);
                break;

            case MaintenanceActionType.Quote:
                var total = (ctx.Request.QuotePartsCost ?? 0) + (ctx.Request.QuoteLaborCost ?? 0);
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.Quoted(subject, total), ct);
                if (ctx.Facts.NeedsOwnerApproval && !ctx.AutoApproved)
                    await _notifications.NotifyAsync(owner, MaintenanceNotifications.QuoteAwaitingApproval(subject, total), ct);
                break;

            case MaintenanceActionType.ApproveQuote:
                await _notifications.NotifyAsync(await BrokerAsync(ctx, ct), MaintenanceNotifications.QuoteApproved(subject), ct);
                break;

            case MaintenanceActionType.RejectQuote:
                var quoteRejected = MaintenanceNotifications.QuoteRejected(subject, message);
                await _notifications.NotifyAsync([.. await BrokerAsync(ctx, ct), .. tenant], quoteRejected, ct);
                break;

            case MaintenanceActionType.AssignTechnician:
            {
                var (visitAr, visitEn) = MaintenanceNotifications.Visit(
                    ctx.Request.ScheduledVisitDate!.Value, ctx.Request.ScheduledVisitTime!.Value);
                var technician = await TechnicianUserAsync(ctx.Request, ct);
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.TechnicianAssigned(
                    subject, ctx.Request.RequestNumber, technician?.Name, visitAr, visitEn), ct);
                if (technician is not null)
                    await _notifications.NotifyAsync([technician.UserId],
                        MaintenanceNotifications.JobAssigned(subject, visitAr, visitEn), ct);
                break;
            }

            case MaintenanceActionType.Reschedule:
            {
                var (visitAr, visitEn) = MaintenanceNotifications.Visit(
                    ctx.Request.ScheduledVisitDate!.Value, ctx.Request.ScheduledVisitTime!.Value);
                var technician = await TechnicianUserAsync(ctx.Request, ct);
                Guid[] recipients = technician is null ? tenant : [.. tenant, technician.UserId];
                await _notifications.NotifyAsync(recipients, MaintenanceNotifications.Rescheduled(subject, visitAr, visitEn), ct);
                break;
            }

            case MaintenanceActionType.Complete:
                await _notifications.NotifyAsync(tenant, MaintenanceNotifications.CompletedToTenant(subject), ct);
                if (ctx.Facts.BrokerHandles)
                    await _notifications.NotifyAsync(owner, MaintenanceNotifications.CompletedToOwner(subject), ct);
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

    private async Task<Guid[]> BrokerAsync(ActionContext ctx, CancellationToken ct)
    {
        var brokerUserId = await _db.Brokers
            .Where(b => b.Id == ctx.Property.BrokerId)
            .Select(b => (Guid?)b.UserId)
            .FirstOrDefaultAsync(ct);

        return brokerUserId is { } id ? [id] : [];
    }

    private async Task<TechnicianRef?> TechnicianUserAsync(MaintenanceRequest request, CancellationToken ct) =>
        await _db.Technicians
            .Where(t => t.Id == request.AssignedTechnicianId)
            .Select(t => new TechnicianRef
            {
                UserId = t.UserId,
                Name = _db.Users.Where(u => u.Id == t.UserId).Select(u => u.FullName).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

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

    private sealed class TechnicianRef
    {
        public Guid UserId { get; init; }
        public string? Name { get; init; }
        public Result<bool>? Failure { get; private init; }

        public static TechnicianRef Failed(Result<bool> failure) => new() { Failure = failure };
    }

    private sealed class ActionContext
    {
        public MaintenanceRequest Request { get; init; } = null!;
        public PropertyRef Property { get; init; } = null!;
        public Guid CallerUserId { get; init; }
        public MaintenanceFacts Facts { get; init; } = null!;
        public MaintenanceStage StageBefore { get; init; }

        // Set by SubmitQuote when the total fell under the owner's limit.
        public bool AutoApproved { get; set; }

        public Result<bool>? Failure { get; private init; }

        public static ActionContext Failed(Result<bool> failure) => new() { Failure = failure };
    }
}
