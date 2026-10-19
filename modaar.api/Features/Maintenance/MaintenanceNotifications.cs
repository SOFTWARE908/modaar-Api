using modaar.api.Features.Notifications.Enums;
using modaar.api.Features.Notifications.Services;

namespace modaar.api.Features.Maintenance;

// Every maintenance notification's wording, in both languages, in one place. The services decide
// WHEN to notify and WHO; this decides WHAT it says.
public static class MaintenanceNotifications
{
    // What every body starts with: "برج النخيل · سباكة". Identifies the request at a glance.
    public sealed record Subject(Guid RequestId, string PropertyTitle, string CategoryAr, string CategoryEn)
    {
        public string Ar => $"{PropertyTitle} · {CategoryAr}";
        public string En => $"{PropertyTitle} · {CategoryEn}";
    }

    // To the owner and the managing broker.
    public static NewNotification Created(Subject s) => New(s,
        "طلب صيانة جديد", "New maintenance request",
        s.Ar, s.En);

    // To the tenant.
    public static NewNotification Accepted(Subject s, string? technicianName) => New(s,
        "تم قبول طلب الصيانة", "Maintenance request accepted",
        technicianName is null ? s.Ar : $"{s.Ar} · الفني: {technicianName}",
        technicianName is null ? s.En : $"{s.En} · Technician: {technicianName}");

    // To the tenant.
    public static NewNotification Rejected(Subject s, string? reason) => New(s,
        "تم رفض طلب الصيانة", "Maintenance request rejected",
        WithDetail(s.Ar, reason), WithDetail(s.En, reason));

    // To the tenant.
    public static NewNotification Inquiry(Subject s, string? message) => New(s,
        "استفسار على طلب الصيانة", "Question about your maintenance request",
        WithDetail(s.Ar, message), WithDetail(s.En, message));

    // To the tenant.
    public static NewNotification DelegatedToTenant(Subject s) => New(s,
        "تم تحويل طلبك إلى الوسيط", "Your request was passed to the broker",
        s.Ar, s.En);

    // To the broker it was handed to.
    public static NewNotification DelegatedToBroker(Subject s) => New(s,
        "طلب صيانة محوّل إليك", "Maintenance request delegated to you",
        s.Ar, s.En);

    // To the tenant.
    public static NewNotification Quoted(Subject s, decimal total) => New(s,
        "عرض سعر جديد", "New price quote",
        $"{s.Ar} · {total:0.##} ر.س", $"{s.En} · SAR {total:0.##}");

    private static NewNotification New(Subject s, string titleAr, string titleEn, string bodyAr, string bodyEn) =>
        new(NotificationType.Maintenance, titleAr, titleEn, bodyAr, bodyEn,
            NotificationTargetType.MaintenanceRequest, s.RequestId);

    // A free-text reason or question goes on its own line; a long one is cut to fit the column.
    private static string WithDetail(string subject, string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? subject
            : $"{subject}\n{(detail.Length > 300 ? detail[..300] + "…" : detail)}";
}
