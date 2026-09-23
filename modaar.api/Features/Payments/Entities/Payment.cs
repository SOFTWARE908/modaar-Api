using modaar.api.Features.Payments.Enums;

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

    public PaymentType Type { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Paid;

    // Who paid. Scoping a tenant's history keys on this.
    public Guid PayerUserId { get; set; }

    // Denormalized so an owner's or broker's history can be scoped without walking the contract.
    public Guid? PropertyId { get; set; }
    public Guid? ContractId { get; set; }

    // Set when the payment settles a maintenance job rather than rent.
    public Guid? MaintenanceRequestId { get; set; }

    // Display text only, e.g. "**** 4679". Never a card number: storing one would pull the
    // project into PCI scope for no benefit, since nothing here charges a card.
    public string? PaymentMethod { get; set; }

    public string? InvoiceUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
