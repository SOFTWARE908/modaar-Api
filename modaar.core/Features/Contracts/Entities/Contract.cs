using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

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

    // Navigation, never lazy-loaded: Include it or project through it.
    public Property Property { get; set; } = null!;
    public User Tenant { get; set; } = null!;
    public Broker? Broker { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }

    // What this tenant actually pays, which can differ from the property's asking rent after a
    // renegotiation or a renewal.
    public decimal InstallmentAmount { get; set; }
    public string Currency { get; set; } = "SAR";

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public PaymentFrequency PaymentFrequency { get; set; } = PaymentFrequency.Monthly;

    public decimal? SecurityDeposit { get; set; }
    public decimal? AnnualRent { get; set; }

    // ── Ejar contract sheet, filled by the admin panel. Null on contracts made before it. ──
    // ContractNumber above is then the Ejar record number (رقم سجل العقد), typed by the admin.

    // From the "contract_type" (new / renewal) and "contract_place" lookups.
    public Guid? ContractTypeId { get; set; }
    public Guid? ContractPlaceId { get; set; }
    // تاريخ إبرام العقد: when the parties signed, as against CreatedAt, when the row was saved.
    public DateOnly? SignedOn { get; set; }

    public decimal? AnnualWaterFee { get; set; }
    // Computed on save: (annual rent + annual water fee) over the contract's months, and how many
    // installments the payment cycle splits that into. Each installment is a Payments row.
    public decimal? TotalValue { get; set; }
    public int? InstallmentsCount { get; set; }
    // Ids from the "payment_channel" lookup (مدى، سداد…).
    public List<Guid> PaymentChannelIds { get; set; } = [];

    // The broker employee who handled it; BrokerId above is their brokerage.
    public Guid? BrokerEmployeeId { get; set; }
    public BrokerEmployee? BrokerEmployee { get; set; }

    // Someone signing on a party's behalf (ممثل المؤجر / ممثل المستأجر). Details only, not accounts.
    public ContractRepresentative? LessorRepresentative { get; set; }
    public ContractRepresentative? TenantRepresentative { get; set; }
    // Set when a contract is ended before EndDate, via POST /contracts/{id}/end.
    public DateOnly? TerminatedOn { get; set; }
    public string? TerminationReason { get; set; }

    public string? PdfUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Scanned pages or supporting photos. Display-only and replaced wholesale, so JSON.
    public List<ContractImage> Images { get; set; } = [];
}

public class ContractRepresentative
{
    public string FullName { get; set; } = null!;
    public Guid? NationalityId { get; set; }
    public Guid? IdTypeId { get; set; }
    public string? IdNumber { get; set; }
    // Saudi mobile as the app stores numbers: 5XXXXXXXX, country code +966.
    public string? PhoneNumber { get; set; }
}

public class ContractImage
{
    public string Url { get; set; } = null!;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}
