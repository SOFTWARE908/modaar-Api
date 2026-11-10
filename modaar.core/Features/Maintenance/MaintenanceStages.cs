using modaar.api.Common.Localization;
using modaar.api.Features.Maintenance.Enums;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Maintenance;

// The facts a request's stage and buttons are worked out from. Built by the list, the details
// screen and the action service alike, so all three read the same request the same way.
public sealed record MaintenanceFacts(
    MaintenanceRequestStatus Status,
    OwnerSubscriptionPlan Plan,
    bool PropertyHasBroker,
    bool QuoteRequired,
    bool HasQuote,
    bool QuoteApproved,
    bool HasTechnician,
    bool IsRated,
    // True on every request before it is completed; after, until the tenant confirms, false.
    bool TenantConfirmed = true)
{
    // A property that has lost its broker can only be run by the owner, whatever the plan said.
    public OwnerSubscriptionPlan EffectivePlan =>
        PropertyHasBroker ? Plan : OwnerSubscriptionPlan.SelfManaged;

    // Who runs the request: the owner on SelfManaged, the managing broker otherwise.
    public bool BrokerHandles => EffectivePlan != OwnerSubscriptionPlan.SelfManaged;

    // Managed is the one plan where the owner signs off the price before work starts.
    public bool NeedsOwnerApproval => EffectivePlan == OwnerSubscriptionPlan.Managed;
}

// Who is looking. A user can be more than one of these on the same request (an owner who is also
// the managing broker), so they are flags, not one value.
public sealed record MaintenanceCaller(bool IsTenant, bool IsOwner, bool IsBroker);

// Stage, next-step label and available actions, derived from stored facts. One place so the list,
// the details screen and the action endpoints can never disagree about a request.
public static class MaintenanceStages
{
    public static MaintenanceStage Derive(MaintenanceFacts f) => f.Status switch
    {
        MaintenanceRequestStatus.Pending => f.BrokerHandles ? MaintenanceStage.Pending : MaintenanceStage.WithOwner,

        MaintenanceRequestStatus.Accepted when f.QuoteRequired && !f.HasQuote => MaintenanceStage.AwaitingQuote,
        MaintenanceRequestStatus.Accepted when f.HasQuote && f.NeedsOwnerApproval && !f.QuoteApproved =>
            MaintenanceStage.AwaitingOwnerApproval,
        MaintenanceRequestStatus.Accepted => f.HasTechnician
            ? MaintenanceStage.TechnicianAssigned
            : MaintenanceStage.ReadyForTechnician,

        MaintenanceRequestStatus.OnTheWay => MaintenanceStage.OnTheWay,
        MaintenanceRequestStatus.InProgress => MaintenanceStage.InProgress,
        MaintenanceRequestStatus.Completed => MaintenanceStage.Completed,
        MaintenanceRequestStatus.Rejected => MaintenanceStage.Rejected,
        _ => MaintenanceStage.Cancelled
    };

    // The buttons the caller gets. The action endpoints check the same list, so a button the app
    // shows always works and one it hides is always refused.
    public static List<MaintenanceAvailableAction> Available(MaintenanceFacts f, MaintenanceCaller caller)
    {
        var actions = new List<MaintenanceAvailableAction>();
        var isHandler = f.BrokerHandles ? caller.IsBroker : caller.IsOwner;

        switch (Derive(f))
        {
            case MaintenanceStage.Pending or MaintenanceStage.WithOwner:
                if (isHandler)
                    actions.AddRange([MaintenanceAvailableAction.Accept, MaintenanceAvailableAction.Reject,
                        MaintenanceAvailableAction.Inquiry]);
                // A SelfManaged owner may still hand one job to their broker.
                if (caller.IsOwner && !f.BrokerHandles && f.PropertyHasBroker)
                    actions.Add(MaintenanceAvailableAction.Delegate);
                break;

            case MaintenanceStage.AwaitingQuote:
                if (isHandler) actions.Add(MaintenanceAvailableAction.SubmitQuote);
                break;

            case MaintenanceStage.AwaitingOwnerApproval:
                if (caller.IsOwner)
                    actions.AddRange([MaintenanceAvailableAction.ApproveQuote, MaintenanceAvailableAction.RejectQuote,
                        MaintenanceAvailableAction.Inquiry]);
                // The broker may revise the price while it waits.
                if (isHandler) actions.Add(MaintenanceAvailableAction.SubmitQuote);
                break;

            case MaintenanceStage.ReadyForTechnician:
                if (isHandler)
                {
                    actions.Add(MaintenanceAvailableAction.AssignTechnician);
                    // An approved quote is final on Managed; elsewhere pricing stays optional.
                    if (!f.NeedsOwnerApproval) actions.Add(MaintenanceAvailableAction.SubmitQuote);
                    // An owner running it themselves may fix it without a listed technician.
                    if (!f.BrokerHandles) actions.Add(MaintenanceAvailableAction.Complete);
                }
                break;

            case MaintenanceStage.TechnicianAssigned:
                if (isHandler)
                {
                    actions.AddRange([MaintenanceAvailableAction.Reschedule, MaintenanceAvailableAction.Complete]);
                    // Pricing after the technician has inspected — except on Managed, where the
                    // owner approved the price before anyone was sent.
                    if (!f.NeedsOwnerApproval) actions.Add(MaintenanceAvailableAction.SubmitQuote);
                }
                break;

            case MaintenanceStage.Completed:
                if (caller.IsTenant && !f.TenantConfirmed) actions.Add(MaintenanceAvailableAction.ConfirmCompletion);
                // Rating follows the tenant's confirmation, and needs a technician to rate.
                if (caller.IsTenant && f.TenantConfirmed && !f.IsRated && f.HasTechnician)
                    actions.Add(MaintenanceAvailableAction.Rate);
                break;
        }

        return actions;
    }

    public static string? NextActionLabel(
        MaintenanceStage stage, DateOnly? visitDate, bool isRated, IRequestLanguage language, bool tenantConfirmed = true)
    {
        return stage switch
        {
            MaintenanceStage.Pending => language.Pick("بانتظار مراجعة الوسيط", "Waiting for the broker to review"),
            MaintenanceStage.WithOwner => language.Pick("بانتظار موافقة المالك", "Waiting for the owner's approval"),
            MaintenanceStage.AwaitingQuote => language.Pick("بانتظار عرض السعر", "Waiting for a price quote"),
            MaintenanceStage.AwaitingOwnerApproval =>
                language.Pick("بانتظار موافقة المالك على العرض", "Waiting for the owner to approve the quote"),
            MaintenanceStage.ReadyForTechnician =>
                language.Pick("بانتظار تعيين فني", "Waiting for a technician to be assigned"),

            MaintenanceStage.TechnicianAssigned or MaintenanceStage.Accepted when visitDate is { } date =>
                language.Pick("زيارة الفني ", "Technician visit on ") + date.ToString("d MMMM", language.Culture),
            MaintenanceStage.TechnicianAssigned or MaintenanceStage.Accepted =>
                language.Pick("بانتظار تحديد موعد الزيارة", "Waiting for a visit time"),

            MaintenanceStage.OnTheWay => language.Pick("الفني في الطريق إليك", "The technician is on the way"),
            MaintenanceStage.InProgress => language.Pick("جارٍ تنفيذ الإصلاح", "The repair is under way"),
            MaintenanceStage.Completed when !tenantConfirmed =>
                language.Pick("أكّد اكتمال العمل", "Confirm the work is done"),
            MaintenanceStage.Completed when !isRated => language.Pick("قيّم الخدمة", "Rate the service"),

            // Rated, rejected or cancelled: nothing left for the tenant to wait on.
            _ => null
        };
    }
}
