using System.Text.Json.Serialization;

namespace modaar.api.Features.Maintenance.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceServiceType
{
    Cleaning,
    PaintRepair,
    Appliances,
    Ac
}
