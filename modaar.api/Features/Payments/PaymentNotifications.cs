using System.Globalization;
using modaar.api.Features.Notifications.Enums;
using modaar.api.Features.Notifications.Services;

namespace modaar.api.Features.Payments;

// Rent reminder wording, both languages. The jobs decide when and who; this decides what it says.
public static class PaymentNotifications
{
    // ar-EG, not ar-SA: ar-SA formats in the Hijri calendar, and due dates are Gregorian.
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    public sealed record Subject(Guid PaymentId, string PropertyTitle, decimal Amount, DateOnly DueDate, string? TenantName)
    {
        public string Ar => $"{PropertyTitle} · {Amount:0.##} ر.س";
        public string En => $"{PropertyTitle} · SAR {Amount:0.##}";
        public string DueAr => DueDate.ToString("d MMMM", Arabic);
        public string DueEn => DueDate.ToString("d MMMM", English);
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

    private static NewNotification New(Subject s, string titleAr, string titleEn, string bodyAr, string bodyEn) =>
        new(NotificationType.Payments, titleAr, titleEn, bodyAr, bodyEn,
            NotificationTargetType.Payment, s.PaymentId);
}
