using modaar.api.Common.Localization;
using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance;

// Stage and the next-step strip, derived from facts already stored on the request. One place so
// the list, the details screen and the dashboard can never label the same request differently.
public static class MaintenanceStages
{
    public static MaintenanceStage Derive(
        MaintenanceRequestStatus status, bool propertyHasBroker, bool quoteRequired, bool hasQuote) => status switch
    {
        // With no managing broker, the owner is the only one who can act on it.
        MaintenanceRequestStatus.Pending => propertyHasBroker ? MaintenanceStage.Pending : MaintenanceStage.WithOwner,

        MaintenanceRequestStatus.Accepted => quoteRequired && !hasQuote
            ? MaintenanceStage.AwaitingQuote
            : MaintenanceStage.Accepted,

        MaintenanceRequestStatus.OnTheWay => MaintenanceStage.OnTheWay,
        MaintenanceRequestStatus.InProgress => MaintenanceStage.InProgress,
        MaintenanceRequestStatus.Completed => MaintenanceStage.Completed,
        MaintenanceRequestStatus.Rejected => MaintenanceStage.Rejected,
        _ => MaintenanceStage.Cancelled
    };

    public static string? NextActionLabel(
        MaintenanceStage stage, bool hasTechnician, DateOnly? visitDate, bool isRated, IRequestLanguage language)
    {
        return stage switch
        {
            MaintenanceStage.Pending => language.Pick("بانتظار مراجعة الوسيط", "Waiting for the broker to review"),
            MaintenanceStage.WithOwner => language.Pick("بانتظار موافقة المالك", "Waiting for the owner's approval"),
            MaintenanceStage.AwaitingQuote => language.Pick("بانتظار عرض السعر", "Waiting for a price quote"),

            MaintenanceStage.Accepted when !hasTechnician =>
                language.Pick("بانتظار تعيين فني", "Waiting for a technician to be assigned"),
            MaintenanceStage.Accepted when visitDate is { } date =>
                language.Pick("زيارة الفني ", "Technician visit on ") + date.ToString("d MMMM", language.Culture),
            MaintenanceStage.Accepted =>
                language.Pick("بانتظار تحديد موعد الزيارة", "Waiting for a visit time"),

            MaintenanceStage.OnTheWay => language.Pick("الفني في الطريق إليك", "The technician is on the way"),
            MaintenanceStage.InProgress => language.Pick("جارٍ تنفيذ الإصلاح", "The repair is under way"),
            MaintenanceStage.Completed when !isRated => language.Pick("قيّم الخدمة", "Rate the service"),

            // Rated, rejected or cancelled: nothing left for the tenant to wait on.
            _ => null
        };
    }
}
