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

    // Present once an invoice has been generated for this payment.
    public string? InvoiceUrl { get; init; }
}
