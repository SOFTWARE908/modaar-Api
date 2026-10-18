namespace modaar.api.Features.Users.Dtos;

// The unit card at the top of the tenant home screen: which unit, what is due, and when.
public record TenantUnitCardDto
{
    public required Guid ContractId { get; init; }
    public required Guid PropertyId { get; init; }
    public string? UnitNumber { get; init; }

    public required string UnitTitle { get; init; }

    public required decimal InstallmentAmount { get; init; }
    public required string Currency { get; init; }

    public required DateOnly DueDate { get; init; }

    // SADAD bill number of the installment shown on the card, for the copy button. Null when
    // none was issued, or when there is no uploaded schedule and DueDate was derived.
    public string? SadadBillNumber { get; init; }

    public required DateOnly ContractEndDate { get; init; }

    public required string PropertyName { get; init; }

    // The "حي النخيل، الرياض · الدور 4" line under the unit name. Any of them may be missing.
    public string? District { get; init; }
    public string? City { get; init; }
    public string? Floor { get; init; }

    public required int DaysOverdue { get; init; }
    // True once DueDate has passed with the installment unpaid, so the card can go red without
    // the app comparing dates against a clock we don't control.
    public required bool IsOverdue { get; init; }
}
