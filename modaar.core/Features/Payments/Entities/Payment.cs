using modaar.api.Features.Contracts.Entities;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Properties.Entities;
using modaar.api.Features.Users.Entities;

namespace modaar.api.Features.Payments.Entities;

// A payment that already happened. The API reads these; nothing here takes money — there is no
// gateway, and the spec has no endpoint that charges anyone. Rows arrive from whatever system
// uploads the portfolio data, same as contracts.
public class Payment
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";

    public DateTimeOffset Date { get; set; }

    // When the money was owed, as against Date, which is when it moved. Null on rows
    // uploaded before the schedule existed.
    public DateOnly? DueDate { get; set; }

    // Which period this installment covers. Dates rather than a label, so the client can
    // format "أكتوبر ٢٠٢٦" or "Oct 2026" from the same row.
    public DateOnly? PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }


    public PaymentType Type { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Paid;

    // Who paid. Scoping a tenant's history keys on this.
    public Guid PayerUserId { get; set; }

    // Denormalized so an owner's or broker's history can be scoped without walking the contract.
    public Guid? PropertyId { get; set; }
    public Guid? ContractId { get; set; }

    // Set when the payment settles a maintenance job rather than rent.
    public Guid? MaintenanceRequestId { get; set; }

    // Navigation, never lazy-loaded: Include it or project through it.
    public User Payer { get; set; } = null!;
    public Property? Property { get; set; }
    public Contract? Contract { get; set; }

    // Display text only, e.g. "**** 4679". Never a card number: storing one would pull the
    // project into PCI scope for no benefit, since nothing here charges a card.
    public string? PaymentMethod { get; set; }

    public string? InvoiceUrl { get; set; }

    // Receipt and processor references shown on the receipt screen, e.g. "RCPT-88214503" and
    // "TXN-88214503". Both arrive with the uploaded row; null until then.
    public string? ReceiptNumber { get; set; }
    public string? TransactionId { get; set; }

    // SADAD bill number the tenant pays this installment with from their bank app. Issued by
    // the biller (the uploading system or the payment provider), never generated here.
    public string? SadadBillNumber { get; set; }

    // Platform service fee on top of Amount. Null when the upload didn't say, which is not the
    // same as zero.
    public decimal? ServiceFee { get; set; }

    // Residential rent is VAT-exempt in Saudi Arabia, but that is the upload's call to make, not
    // ours: null means "not stated".
    public bool? IsVatExempt { get; set; }
    public decimal? VatAmount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
