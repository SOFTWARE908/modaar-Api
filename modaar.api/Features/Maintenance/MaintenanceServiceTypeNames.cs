using modaar.api.Common.Localization;
using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance;

// Turns a MaintenanceServiceType into the title shown on a card. Without this the list and the
// property history render "PaintRepair" straight to the user, which is what they do today.
//
// A lookup in code rather than a resource file or a table: the set is fixed, it changes only
// when the enum changes, and a missing entry should be a compile-time concern rather than a
// silently blank card.
public static class MaintenanceServiceTypeNames
{
    private static readonly Dictionary<MaintenanceServiceType, (string Ar, string En)> Names = new()
    {
        [MaintenanceServiceType.Cleaning]    = ("تنظيف",           "Cleaning"),
        [MaintenanceServiceType.PaintRepair] = ("دهان وترميم",     "Paint & repair"),
        [MaintenanceServiceType.Appliances]  = ("أجهزة منزلية",    "Appliances"),
        [MaintenanceServiceType.Ac]          = ("تكييف",           "Air conditioning")
    };

    public static string Title(MaintenanceServiceType type, IRequestLanguage language) =>
        Names.TryGetValue(type, out var name)
            ? language.Pick(name.Ar, name.En)
            // A new enum member with no entry falls back to its own name rather than an empty
            // card — visibly wrong, which is the point.
            : type.ToString();
}

// The slot label that appears next to a visit date: "صباحاً" / "Morning".
public static class MaintenanceTimeSlotNames
{
    private static readonly Dictionary<MaintenanceTimeSlot, (string Ar, string En)> Names = new()
    {
        [MaintenanceTimeSlot.Morning]   = ("صباحاً", "Morning"),
        [MaintenanceTimeSlot.Afternoon] = ("ظهراً",  "Afternoon"),
        [MaintenanceTimeSlot.Evening]   = ("مساءً",  "Evening")
    };

    public static string Label(MaintenanceTimeSlot slot, IRequestLanguage language) =>
        Names.TryGetValue(slot, out var name) ? language.Pick(name.Ar, name.En) : slot.ToString();
}
