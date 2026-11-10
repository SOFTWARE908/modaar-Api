using modaar.api.Features.Properties.Enums;

namespace modaar.api.Features.Properties.Dtos;

// The condition a unit was handed over in for the current tenancy. Becomes the reference point
// at move-out for deciding whether damage is the tenant's or predates them.
public record PropertyHandoverDto
{
    public required Guid Id { get; init; }

    public required Guid PropertyId { get; init; }

    // Null on a draft started before a lease was attached.
    public Guid? ContractId { get; init; }

    public required IReadOnlyList<string> Images { get; init; }
    public string? Description { get; init; }

    // Set when the handover is signed off, not when the draft is started.
    public DateOnly? HandoverDate { get; init; }

    public required PropertyHandoverStatus Status { get; init; }

    public string? SignatureUrl { get; init; }
}
