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

    public required IReadOnlyList<InvoiceBreakdownItemDto> Items { get; init; }

    public string? InvoiceUrl { get; init; }
}
