using System.Globalization;
using modaar.api.Features.Notifications.Enums;
using modaar.api.Features.Notifications.Services;

namespace modaar.api.Features.Payments;

// Payment notification wording, both languages: rent reminders, receipts and bank-transfer
// review. The jobs and services decide when and who; this decides what it says.
public static class PaymentNotifications
{
    // ar-EG, not ar-SA: ar-SA formats in the Hijri calendar, and due dates are Gregorian.
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    public sealed record Subject(Guid PaymentId, string PropertyTitle, decimal Amount, DateOnly? DueDate, string? TenantName)
    {
        public string Ar => $"{PropertyTitle} · {Amount:0.##} ر.س";
        public string En => $"{PropertyTitle} · SAR {Amount:0.##}";
        public string DueAr => DueDate?.ToString("d MMMM", Arabic) ?? string.Empty;
        public string DueEn => DueDate?.ToString("d MMMM", English) ?? string.Empty;
    }

    // To the tenant, in the week before.
    public static NewNotification Upcoming(Subject s) => New(s,
        "تذكير بموعد قسط الإيجار", "Rent due soon",
        $"{s.Ar} · يستحق {s.DueAr}", $"{s.En} · due {s.DueEn}");

    // To the tenant, on the day.
    public static NewNotification DueToday(Subject s) => New(s,
        "قسط الإيجار مستحق اليوم", "Rent is due today",
        s.Ar, s.En);

    // To the tenant, the day after and then weekly until paid.
    public static NewNotification OverdueToTenant(Subject s) => New(s,
        "قسط الإيجار متأخر", "Rent payment overdue",
        $"{s.Ar} · كان مستحقاً {s.DueAr}", $"{s.En} · was due {s.DueEn}");

    // To the owner or broker running the property, on the same days.
    public static NewNotification OverdueToHandler(Subject s) => New(s,
        "قسط إيجار متأخر", "Overdue rent",
        $"{s.TenantName} · {s.Ar} · كان مستحقاً {s.DueAr}", $"{s.TenantName} · {s.En} · was due {s.DueEn}");

    // To the tenant, once a card charge or a bank transfer is confirmed.
    public static NewNotification Received(Subject s) => New(s,
        "تم استلام دفعتك", "Payment received",
        s.Ar, s.En);

    // To the owner or broker running the property: a receipt is waiting for them.
    public static NewNotification TransferToReview(Subject s) => New(s,
        "إيصال تحويل بانتظار المراجعة", "Bank transfer to review",
        $"{s.TenantName} · {s.Ar}", $"{s.TenantName} · {s.En}");

    // To the tenant: the receipt was turned down; the installment is owed again.
    public static NewNotification TransferRejected(Subject s, string reason) => New(s,
        "لم يتم قبول إيصال التحويل", "Bank transfer not accepted",
        $"{s.Ar}\n{reason}", $"{s.En}\n{reason}");

    // To the tenant, sent by hand from the owner's or broker's "إرسال تذكير بالسداد".
    public static NewNotification ManualReminder(Subject s, string? senderName) => New(s,
        "تذكير بسداد الإيجار", "Rent payment reminder",
        $"{s.Ar} · يستحق {s.DueAr}" + (senderName is null ? "" : $"\nمن {senderName}"),
        $"{s.En} · due {s.DueEn}" + (senderName is null ? "" : $"\nfrom {senderName}"));

    private static NewNotification New(Subject s, string titleAr, string titleEn, string bodyAr, string bodyEn) =>
        new(NotificationType.Payments, titleAr, titleEn, bodyAr, bodyEn,
            NotificationTargetType.Payment, s.PaymentId);
}
