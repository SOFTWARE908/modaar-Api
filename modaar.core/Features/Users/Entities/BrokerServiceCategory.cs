namespace modaar.api.Features.Users.Entities;

// What a brokerage offers (إدارة أملاك، تسويق وتأجير، تجاري، صيانة): one list for everyone,
// managed by the admin, so the marketplace can filter on it. Delete = deactivate.
public class BrokerServiceCategory
{
    public Guid Id { get; set; }

    // Stable key for the app and the filter: "property_management".
    public string Code { get; set; } = null!;

    public string NameAr { get; set; } = null!;
    public string NameEn { get; set; } = null!;
    public string? IconUrl { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// A brokerage offers a service. Chosen by the broker from the active categories.
public class BrokerOfferedService
{
    public Guid BrokerId { get; set; }
    public Guid CategoryId { get; set; }
}
