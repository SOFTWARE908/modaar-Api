using System.Text.Json.Serialization;
using FluentValidation;
using modaar.api.Common.Dtos;
using modaar.api.Features.Agreements.Entities;

namespace modaar.api.Features.Agreements.Dtos;

// ── Input ──────────────────────────────────────────────────────────────────

// The owner sends a mandate to a broker. PropertyIds omitted → all the owner's units.
// AutoApproveLimit omitted → every maintenance quote still comes to the owner.
public record CreateAgreementDto
{
    public Guid BrokerId { get; init; }
    public IReadOnlyList<Guid>? PropertyIds { get; init; }
    public decimal? AutoApproveLimit { get; init; }
}

// The same, from the broker's profile (POST /api/brokers/{brokerId}/management-requests), where
// the broker is in the route.
public record RequestManagementDto
{
    public IReadOnlyList<Guid>? PropertyIds { get; init; }
    public decimal? AutoApproveLimit { get; init; }
}

// Owner. Anything omitted is copied from the agreement being renewed (its units: the ones it
// holds now, or held when it ended).
public record RenewAgreementDto
{
    public IReadOnlyList<Guid>? PropertyIds { get; init; }
    public decimal? AutoApproveLimit { get; init; }
}

public record AgreementReasonDto
{
    public string Reason { get; init; } = string.Empty;
}

public record AgreementsRequestDto : PagedRequestDto
{
    public ManagementAgreementStatus? Status { get; init; }
}

// ── Output ─────────────────────────────────────────────────────────────────

// How the app shows an agreement. Active splits into endingSoon when its end is 30 days or less
// away.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgreementDisplayStatus
{
    Pending,
    Active,
    EndingSoon,
    Expired,
    Terminated,
    Rejected,
    Withdrawn
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgreementAction
{
    // Broker, on a pending one.
    Accept,
    Reject,
    // Owner, on a pending one.
    Withdraw,
    // Either side, on an active one.
    Cancel,
    // Owner, on an active or expired one.
    Renew
}

public record AgreementPartyDto
{
    public required Guid UserId { get; init; }
    public string? Name { get; init; }
    public string? PhoneNumber { get; init; }
    public string? ImageUrl { get; init; }
}

public record AgreementListItemDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required ManagementAgreementStatus Status { get; init; }
    public required AgreementDisplayStatus DisplayStatus { get; init; }

    // The other side: the broker for an owner, the owner for a broker.
    public required AgreementPartyDto OtherParty { get; init; }

    // Units it holds now (the units sent while pending).
    public required int UnitsCount { get; init; }

    public decimal? AutoApproveLimit { get; init; }
    public DateOnly? StartDate { get; init; }

    // Active: when the last unit leaves (null while a vacant unit has no lease yet).
    // Ended: the day it ended.
    public DateOnly? EndDate { get; init; }
    public int? DaysLeft { get; init; }

    public bool IsRenewal { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public record AgreementDetailsDto
{
    public required Guid Id { get; init; }
    public required string Number { get; init; }
    public required ManagementAgreementStatus Status { get; init; }
    public required AgreementDisplayStatus DisplayStatus { get; init; }

    public required Guid BrokerId { get; init; }
    public required AgreementPartyDto Broker { get; init; }
    public required AgreementPartyDto Owner { get; init; }

    // Maintenance quotes at or under this skip the owner on Managed units. Null: every quote goes
    // to the owner.
    public decimal? AutoApproveLimit { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int? DaysLeft { get; init; }
    public DateOnly? EndedOn { get; init; }
    public string? EndReason { get; init; }

    public Guid? RenewedFromId { get; init; }
    public string? RenewedFromNumber { get; init; }

    public required IReadOnlyList<AgreementUnitDto> Units { get; init; }
    public required IReadOnlyList<AgreementEventDto> Events { get; init; }

    // What the caller may do now.
    public required IReadOnlyList<AgreementAction> AvailableActions { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public record AgreementUnitDto
{
    public required Guid PropertyId { get; init; }
    public required string Title { get; init; }
    public string? UnitNumber { get; init; }
    public string? BuildingName { get; init; }
    public string? TenantName { get; init; }

    // Null while vacant: the unit stays until the first lease signed on it ends.
    public DateOnly? EndsOn { get; init; }
    public int? DaysLeft { get; init; }

    public DateOnly? ExitedOn { get; init; }
    public AgreementUnitExitReason? ExitReason { get; init; }
}

public record AgreementEventDto
{
    public required AgreementEventType Type { get; init; }
    public required DateTimeOffset At { get; init; }
    public string? ActorName { get; init; }
    public string? PropertyTitle { get; init; }
    public string? Note { get; init; }
}

// ── Validation ─────────────────────────────────────────────────────────────

public static class AgreementRules
{
    public const decimal MaxAutoApproveLimit = 1_000_000m;

    public static void Limit<T>(AbstractValidator<T> v, System.Linq.Expressions.Expression<Func<T, decimal?>> limit)
    {
        var get = limit.Compile();
        v.RuleFor(limit)
            .GreaterThanOrEqualTo(0m).LessThan(MaxAutoApproveLimit)
            .When(x => get(x) is not null);
    }
}

public sealed class CreateAgreementValidator : AbstractValidator<CreateAgreementDto>
{
    public CreateAgreementValidator()
    {
        RuleFor(x => x.BrokerId).NotEmpty();
        AgreementRules.Limit(this, x => x.AutoApproveLimit);
    }
}

public sealed class RequestManagementValidator : AbstractValidator<RequestManagementDto>
{
    public RequestManagementValidator() => AgreementRules.Limit(this, x => x.AutoApproveLimit);
}

public sealed class RenewAgreementValidator : AbstractValidator<RenewAgreementDto>
{
    public RenewAgreementValidator() => AgreementRules.Limit(this, x => x.AutoApproveLimit);
}

public sealed class AgreementReasonValidator : AbstractValidator<AgreementReasonDto>
{
    public AgreementReasonValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}
