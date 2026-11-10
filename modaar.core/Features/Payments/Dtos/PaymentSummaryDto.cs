namespace modaar.api.Features.Payments.Dtos;

// The two cards above the payments list. Computed here because the list is paged, so the app
// never holds every row.
public record PaymentSummaryDto
{
    // Settled this calendar year (Riyadh time), by the date the money moved.
    public required decimal PaidThisYear { get; init; }

    // Rent installments still unpaid on the caller's active contracts, overdue ones included.
    // Counts uploaded installment rows only, so it reads 0 until a contract's schedule exists.
    public required decimal RemainingOnContract { get; init; }

    public required string Currency { get; init; }
}
