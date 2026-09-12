using modaar.api.Features.Contracts.Enums;

namespace modaar.api.Features.Contracts.Entities;

// The lease. This is what makes a user a tenant and a property rented — neither of those is
// stored anywhere else.
public class Contract
{
    public Guid Id { get; set; }

    // Human-readable reference shown in the app and on the PDF. Unique, generated at creation.
    public string ContractNumber { get; set; } = null!;

    public Guid PropertyId { get; set; }
    public Guid TenantUserId { get; set; }

    // Snapshot of the managing brokerage at signing. Deliberately not read through the property:
    // the owner can reassign the property to a different broker without rewriting old leases.
    public Guid? BrokerId { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }

    // What this tenant actually pays, which can differ from the property's asking rent after a
    // renegotiation or a renewal.
    public decimal MonthlyAmount { get; set; }
    public string Currency { get; set; } = "SAR";

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Active;

    // Set when a contract is ended before EndDate, via POST /contracts/{id}/end.
    public DateOnly? TerminatedOn { get; set; }
    public string? TerminationReason { get; set; }

    public string? PdfUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Scanned pages or supporting photos. Display-only and replaced wholesale, so JSON.
    public List<ContractImage> Images { get; set; } = [];
}

public class ContractImage
{
    public string Url { get; set; } = null!;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}
