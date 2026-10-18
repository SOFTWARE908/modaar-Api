using modaar.api.Common.Localization;
using modaar.api.Features.Maintenance.Enums;

namespace modaar.api.Features.Maintenance;

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
