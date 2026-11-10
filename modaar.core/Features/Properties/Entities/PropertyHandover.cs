using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Properties.Entities;

// The handover record for one tenancy: the condition the unit was given over in. Its own table
// rather than columns on Property because a new tenancy produces a new record and the old one
// is the evidence of what the last tenant received.
public class PropertyHandover
{
    public Guid Id { get; set; }

    public Guid PropertyId { get; set; }

    // No foreign key yet — Contracts does not exist. Add the FK with that migration.
    public Guid? ContractId { get; set; }

    public string? Description { get; set; }

    public PropertyHandoverStatus Status { get; set; } = PropertyHandoverStatus.Draft;

    // Set when the handover is signed off, not when the draft is started.
    public DateOnly? HandoverDate { get; set; }

    public string? SignatureUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<PropertyImage> Images { get; set; } = [];
}
