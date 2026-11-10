namespace modaar.api.Features.Payments.Dtos;

public record InvoiceBreakdownItemDto
{
    public required string Label { get; init; }
    public required decimal Amount { get; init; }
}
