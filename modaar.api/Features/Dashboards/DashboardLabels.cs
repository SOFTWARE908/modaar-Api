using modaar.api.Common.Localization;

namespace modaar.api.Features.Dashboards;

// Labels on the home-screen tiles. Hardcoded English strings in DashboardService today.
public static class DashboardLabels
{
    public static string Units(IRequestLanguage l) => l.Pick("الوحدات", "Units");

    public static string UnitsManaged(IRequestLanguage l) =>
        l.Pick("وحدات تحت الإدارة", "Units managed");

    public static string ActiveContracts(IRequestLanguage l) =>
        l.Pick("عقود نشطة", "Active contracts");

    public static string CollectionRate(IRequestLanguage l) =>
        l.Pick("نسبة التحصيل", "Collection rate");

    public static string ThisMonth(IRequestLanguage l) => l.Pick("هذا الشهر", "This month");
}
