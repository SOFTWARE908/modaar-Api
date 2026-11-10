namespace modaar.api.Features.Dashboards.Dtos;

// A recent money movement shown on the owner and broker home screens. Sourced from payments,
// which do not exist yet — the previews come back empty until that slice lands.
public record TransactionPreviewDto
{
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }

    public required string Status { get; init; }

    public string? SenderName { get; init; }
    public string? PropertyName { get; init; }

    public required DateTimeOffset Date { get; init; }

    public Guid? InvoiceId { get; init; }
}
