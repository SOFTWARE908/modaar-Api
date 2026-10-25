using System.Globalization;
using modaar.api.Features.Notifications.Enums;
using modaar.api.Features.Notifications.Services;

namespace modaar.api.Features.Agreements;

// Management agreement (تفويض) wording, both languages. All open the agreement except the new
// request, which has no agreement yet.
public static class AgreementNotifications
{
    // ar-EG, not ar-SA: ar-SA formats in the Hijri calendar, and these dates are Gregorian.
    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    // To the broker.
    public static NewNotification NewRequest(string? ownerName) =>
        new(NotificationType.Contracts,
            "طلب إدارة أملاك جديد", "New management request",
            $"{ownerName} يطلب إدارة أملاكه", $"{ownerName} asked you to manage their properties");

    // To the owner.
    public static NewNotification RequestRejected(string? brokerName) =>
        new(NotificationType.Contracts,
            "تم رفض طلب الإدارة", "Management request declined",
            $"{brokerName} اعتذر عن طلبك", $"{brokerName} declined your request");

    // To the owner: new terms or a renewal to approve.
    public static NewNotification AwaitingOwner(Guid id, string number, string? brokerName, bool renewal) => New(id,
        renewal ? "طلب تجديد التفويض" : "تفويض بانتظار موافقتك",
        renewal ? "Agreement renewal to approve" : "Agreement awaiting your approval",
        $"{brokerName} · {number}", $"{brokerName} · {number}");

    // To the broker.
    public static NewNotification Approved(Guid id, string number, string? ownerName) => New(id,
        "تمت الموافقة على التفويض", "Agreement approved",
        $"{ownerName} · {number}", $"{ownerName} · {number}");

    public static NewNotification Rejected(Guid id, string number, string? ownerName) => New(id,
        "تم رفض التفويض", "Agreement declined",
        $"{ownerName} · {number}", $"{ownerName} · {number}");

    // To the owner.
    public static NewNotification Withdrawn(Guid id, string number, string? brokerName) => New(id,
        "تم سحب التفويض", "Agreement withdrawn",
        $"{brokerName} · {number}", $"{brokerName} · {number}");

    // To the other side.
    public static NewNotification Terminated(Guid id, string number, string? byName) => New(id,
        "تم إلغاء التفويض", "Agreement cancelled",
        $"{byName} · {number}", $"{byName} · {number}");

    // To both.
    public static NewNotification UnitLeaving(Guid id, string number, string unitTitle, DateOnly endsOn) => New(id,
        "وحدة ستخرج من التفويض قريباً", "A unit is leaving the agreement soon",
        $"{unitTitle} · {endsOn.ToString("d MMMM yyyy", Arabic)} · {number}",
        $"{unitTitle} · {endsOn.ToString("d MMMM yyyy", English)} · {number}");

    public static NewNotification UnitLeft(Guid id, string number, string unitTitle) => New(id,
        "خرجت وحدة من التفويض", "A unit left the agreement",
        $"{unitTitle} · {number}", $"{unitTitle} · {number}");

    public static NewNotification Expired(Guid id, string number) => New(id,
        "انتهى التفويض", "Agreement ended",
        $"خرجت كل الوحدات · {number}", $"All units have left · {number}");

    private static NewNotification New(Guid id, string titleAr, string titleEn, string bodyAr, string bodyEn) =>
        new(NotificationType.Contracts, titleAr, titleEn, bodyAr, bodyEn,
            NotificationTargetType.ManagementAgreement, id);
}
