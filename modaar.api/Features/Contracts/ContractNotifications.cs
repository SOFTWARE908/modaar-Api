using System.Globalization;
using modaar.api.Features.Notifications.Enums;
using modaar.api.Features.Notifications.Services;

namespace modaar.api.Features.Contracts;

// Lease renewal reminder wording, both languages.
public static class ContractNotifications
{
    // ar-EG, not ar-SA: ar-SA formats in the Hijri calendar, and lease dates are Gregorian.
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    public sealed record Subject(Guid ContractId, string PropertyTitle, DateOnly EndDate, string? TenantName)
    {
        public string EndAr => EndDate.ToString("d MMMM yyyy", Arabic);
        public string EndEn => EndDate.ToString("d MMMM yyyy", English);
    }

    // To the tenant.
    public static NewNotification RenewalToTenant(Subject s) => New(s,
        "اقترب موعد تجديد عقدك", "Your lease renewal is coming up",
        $"{s.PropertyTitle} · ينتهي العقد {s.EndAr}", $"{s.PropertyTitle} · lease ends {s.EndEn}");

    // To the owner or broker running the property.
    public static NewNotification RenewalToHandler(Subject s) => New(s,
        "عقد يقترب من الانتهاء", "Lease ending soon",
        $"{s.TenantName} · {s.PropertyTitle} · ينتهي {s.EndAr}", $"{s.TenantName} · {s.PropertyTitle} · ends {s.EndEn}");

    private static NewNotification New(Subject s, string titleAr, string titleEn, string bodyAr, string bodyEn) =>
        new(NotificationType.Contracts, titleAr, titleEn, bodyAr, bodyEn,
            NotificationTargetType.Contract, s.ContractId);
}
