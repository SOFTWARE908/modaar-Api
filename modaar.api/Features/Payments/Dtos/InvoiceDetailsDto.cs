namespace modaar.api.Features.Payments.Dtos;

public record InvoiceDetailsDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }

    public required decimal TotalAmount { get; init; }
    public required string Currency { get; init; }

    public required DateTimeOffset Date { get; init; }

    // Masked display text, e.g. "**** 4679".
    public string? PaymentMethod { get; init; }

    // "RCPT-88214503" / "TXN-88214503". Null until the uploaded row carries them.
    public string? ReceiptNumber { get; init; }
    public string? TransactionId { get; init; }

    // The period this payment covers; same values as on the list row.
    public DateOnly? PeriodStart { get; init; }
    public DateOnly? PeriodEnd { get; init; }

    // Null means the upload didn't state it, not zero / not exempt.
    public decimal? ServiceFee { get; init; }
    public bool? IsVatExempt { get; init; }
    public decimal? VatAmount { get; init; }

    // The receipt header, so the screen doesn't have to fetch them from other endpoints.
    // TenantName is null until the payer completes their profile.
    public string? TenantName { get; init; }
    public string? UnitLabel { get; init; }
    public string? ContractNumber { get; init; }

    public required IReadOnlyList<InvoiceBreakdownItemDto> Items { get; init; }

    public string? InvoiceUrl { get; init; }

    // pending · paid · underReview — whether there is anything left to pay.
    public required modaar.api.Features.Payments.Enums.PaymentStatus Status { get; init; }

    // What the tenant is charged when paying: amount + service fee + VAT.
    public required decimal AmountDue { get; init; }

    // The latest bank transfer on this payment (under review, rejected or approved), or null.
    public BankTransferDto? BankTransfer { get; init; }

    // True for the owner or broker running the property while a transfer waits for them:
    // show approve / reject.
    public required bool CanReviewTransfer { get; init; }
}
