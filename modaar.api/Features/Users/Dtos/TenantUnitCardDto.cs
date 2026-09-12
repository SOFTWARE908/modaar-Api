namespace modaar.api.Features.Users.Dtos;

// The unit card at the top of the tenant home screen: which unit, what is due, and when.
public record TenantUnitCardDto
{
    public required Guid ContractId { get; init; }
    public required Guid PropertyId { get; init; }

    public required string UnitTitle { get; init; }

    public required decimal InstallmentAmount { get; init; }
    public required string Currency { get; init; }

    public required DateOnly DueDate { get; init; }

    // True once DueDate has passed with the installment unpaid, so the card can go red without
    // the app comparing dates against a clock we don't control.
    public required bool IsOverdue { get; init; }
}
