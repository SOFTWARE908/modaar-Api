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

    // To whoever runs it: the owner on SelfManaged, the managing broker otherwise.
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

    // To the owner (Managed plan): the broker priced it and the work waits on them.
    public static NewNotification QuoteAwaitingApproval(Subject s, decimal total) => New(s,
        "طلب صيانة بانتظار موافقتك", "Maintenance request awaiting your approval",
        $"{s.Ar} · {total:0.##} ر.س", $"{s.En} · SAR {total:0.##}");

    // To the managing broker.
    public static NewNotification QuoteApproved(Subject s) => New(s,
        "وافق المالك على عرض السعر", "The owner approved the quote",
        s.Ar, s.En);

    // To the managing broker and the tenant: a rejected quote closes the request.
    public static NewNotification QuoteRejected(Subject s, string? reason) => New(s,
        "رفض المالك عرض السعر", "The owner rejected the quote",
        WithDetail(s.Ar, reason), WithDetail(s.En, reason));

    // To the managing broker: the owner asked about the quote before deciding.
    public static NewNotification OwnerInquiry(Subject s, string? message) => New(s,
        "استفسار من المالك", "Question from the owner",
        WithDetail(s.Ar, message), WithDetail(s.En, message));

    // To the tenant.
    public static NewNotification TechnicianAssigned(Subject s, string requestNumber, string? technicianName, string visitAr, string visitEn) => New(s,
        "تم تعيين فني لطلبك", "A technician has been assigned",
        $"{technicianName} سيزورك {visitAr} · طلب {requestNumber}",
        $"{technicianName} will visit {visitEn} · request {requestNumber}");

    // To the technician.
    public static NewNotification JobAssigned(Subject s, string visitAr, string visitEn) => New(s,
        "مهمة صيانة جديدة", "New maintenance job",
        $"{s.Ar} · {visitAr}", $"{s.En} · {visitEn}");

    // To the tenant and the technician.
    public static NewNotification Rescheduled(Subject s, string visitAr, string visitEn) => New(s,
        "تم تغيير موعد الزيارة", "The visit was rescheduled",
        $"{s.Ar} · {visitAr}", $"{s.En} · {visitEn}");

    // To the tenant.
    public static NewNotification CompletedToTenant(Subject s) => New(s,
        "تم إصلاح العطل", "The repair is done",
        $"{s.Ar} · راجع التقرير وأكّد اكتمال العمل", $"{s.En} · Check the report and confirm the work is done");

    // To whoever ran the job.
    public static NewNotification ConfirmedByTenant(Subject s) => New(s,
        "أكّد المستأجر اكتمال العمل", "The tenant confirmed the work is done",
        s.Ar, s.En);

    // To the owner when the broker ran the job.
    public static NewNotification CompletedToOwner(Subject s) => New(s,
        "اكتمل طلب الصيانة", "Maintenance request completed",
        s.Ar, s.En);

    // "الأحد 26 أكتوبر الساعة 14:30" / "Sunday 26 October at 14:30". ar-EG, not ar-SA: ar-SA
    // formats in the Hijri calendar, and the visit was picked on a Gregorian date.
    public static (string Ar, string En) Visit(DateOnly date, TimeOnly time) => (
        $"{date.ToString("dddd d MMMM", System.Globalization.CultureInfo.GetCultureInfo("ar-EG"))} الساعة {time:HH:mm}",
        $"{date.ToString("dddd d MMMM", System.Globalization.CultureInfo.InvariantCulture)} at {time:HH:mm}");

    private static NewNotification New(Subject s, string titleAr, string titleEn, string bodyAr, string bodyEn) =>
        new(NotificationType.Maintenance, titleAr, titleEn, bodyAr, bodyEn,
            NotificationTargetType.MaintenanceRequest, s.RequestId);

    // A free-text reason or question goes on its own line; a long one is cut to fit the column.
    private static string WithDetail(string subject, string? detail) =>
        string.IsNullOrWhiteSpace(detail)
            ? subject
            : $"{subject}\n{(detail.Length > 300 ? detail[..300] + "…" : detail)}";
}
