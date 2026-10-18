using modaar.api.Features.Payments.Enums;

namespace modaar.api.Features.Payments.Dtos;

// A row in payment history, and in the tenant dashboard preview.
public record PaymentDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }

    public required decimal Amount { get; init; }
    public required string Currency { get; init; }

    public required DateTimeOffset Date { get; init; }
    public required PaymentType Type { get; init; }

    // Paid, Pending or Failed. 
    public required PaymentStatus Status { get; init; }

    public required PaymentDisplayStatus DisplayStatus { get; init; }

    // Days past DueDate, or 0 when nothing is late.
    public required int DaysOverdue { get; init; }

    // When it was owed. Null on historical rows uploaded without a schedule.
    public DateOnly? DueDate { get; init; }

    // The period covered, for the "أكتوبر ٢٠٢٦" line under the title.
    public DateOnly? PeriodStart { get; init; }
    public DateOnly? PeriodEnd { get; init; }

    // Platform fee on top of Amount, shown on the pay screen for an unpaid installment. Null
    // when the upload didn't state it.
    public decimal? ServiceFee { get; init; }

    // SADAD bill number for paying this installment from a bank app. Null until the biller
    // has issued one.
    public string? SadadBillNumber { get; init; }

    // Masked display text, e.g. "**** 4679". Was on the invoice only.
    public string? PaymentMethod { get; init; }
    // Present once an invoice has been generated for this payment.
    public string? InvoiceUrl { get; init; }
}
