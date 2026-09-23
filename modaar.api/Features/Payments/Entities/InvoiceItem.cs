namespace modaar.api.Features.Payments.Entities;

// A line on an invoice — base rent, service fee, utilities, tax. A real table rather than a JSON
// column because the amounts are money: they get summed, reported on, and reconciled against the
// payment total, none of which a JSON blob supports well.
public class InvoiceItem
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }

    public string Label { get; set; } = null!;
    public decimal Amount { get; set; }

    public int SortOrder { get; set; }
}
