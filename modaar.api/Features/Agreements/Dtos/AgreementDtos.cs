using System.Text.Json.Serialization;
using FluentValidation;
using modaar.api.Common.Dtos;
using modaar.api.Features.Agreements.Entities;
using modaar.api.Features.Users.Enums;

namespace modaar.api.Features.Agreements.Dtos;

// ── Input ──────────────────────────────────────────────────────────────────

// What the broker writes when proposing an agreement.
public record AgreementTermsDto
{
    // Percent of the rent collected.
    public decimal ManagementFeePercent { get; init; }

    // Both or neither.
    public LeasingFeeType? LeasingFeeType { get; init; }
    public decimal? LeasingFeeValue { get; init; }

    public string? DocumentUrl { get; init; }
}

// Broker answers an owner's request with terms. PropertyIds narrows the request's units;
// omitted → the units the owner asked for (all of them if they asked for the whole portfolio).
public record AcceptManagementRequestDto : AgreementTermsDto
{
    public IReadOnlyList<Guid>? PropertyIds { get; init; }
}

// Broker proposes an agreement without a request. PropertyIds omitted → all the owner's units.
public record CreateAgreementDto : AgreementTermsDto
{
    public Guid OwnerUserId { get; init; }
    public IReadOnlyList<Guid>? PropertyIds { get; init; }
}

// Renewal: anything omitted is copied from the agreement being renewed.
public record RenewAgreementDto
{
    public decimal? ManagementFeePercent { get; init; }
    public LeasingFeeType? LeasingFeeType { get; init; }
    public decimal? LeasingFeeValue { get; init; }
    public string? DocumentUrl { get; init; }
}

public record AgreementReasonDto
{
    public string Reason { get; init; } = string.Empty;
}

public record AgreementsRequestDto : PagedRequestDto
{
    public ManagementAgreementStatus? Status { get; init; }
}

public record ManagementRequestsRequestDto : PagedRequestDto
{
    // Default: pending only.
    public BrokerManagementRequestStatus? Status { get; init; }
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
    Approve,
    Reject,
    Withdraw,
    Cancel,
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

    // Units it holds now (proposed units while pending).
    public required int UnitsCount { get; init; }

    public decimal? ManagementFeePercent { get; init; }
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

    public decimal? ManagementFeePercent { get; init; }
    public LeasingFeeType? LeasingFeeType { get; init; }
    public decimal? LeasingFeeValue { get; init; }
    public string? DocumentUrl { get; init; }

    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int? DaysLeft { get; init; }
    public DateOnly? EndedOn { get; init; }
    public string? EndReason { get; init; }

    public Guid? RenewedFromId { get; init; }
    public string? RenewedFromNumber { get; init; }
    public Guid? RequestId { get; init; }

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

public record ManagementRequestItemDto
{
    public required Guid Id { get; init; }
    public required BrokerManagementRequestStatus Status { get; init; }
    public required AgreementPartyDto Owner { get; init; }

    // True when the owner asked for their whole portfolio.
    public required bool WholePortfolio { get; init; }
    public required int UnitsCount { get; init; }

    public string? DecisionReason { get; init; }
    public Guid? AgreementId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

// ── Validation ─────────────────────────────────────────────────────────────

public static class AgreementRules
{
    public static void Terms<T>(AbstractValidator<T> v) where T : AgreementTermsDto
    {
        v.RuleFor(x => x.ManagementFeePercent).InclusiveBetween(0m, 100m);
        Fees(v, x => x.LeasingFeeType, x => x.LeasingFeeValue);
        v.RuleFor(x => x.DocumentUrl).Must(BeUrl).MaximumLength(500).When(x => x.DocumentUrl is not null)
            .WithMessage("documentUrl must be an http(s) URL.");
    }

    public static void Fees<T>(AbstractValidator<T> v,
        System.Linq.Expressions.Expression<Func<T, LeasingFeeType?>> type,
        System.Linq.Expressions.Expression<Func<T, decimal?>> value)
    {
        var getType = type.Compile();
        var getValue = value.Compile();

        v.RuleFor(x => x)
            .Must(x => getType(x) is null == getValue(x) is null)
            .WithName("leasingFee")
            .WithMessage("leasingFeeType and leasingFeeValue go together.");

        v.RuleFor(value).GreaterThanOrEqualTo(0m).When(x => getValue(x) is not null);
        v.RuleFor(value).LessThanOrEqualTo(100m)
            .When(x => getType(x) == LeasingFeeType.Percent && getValue(x) is not null)
            .WithMessage("A percent leasing fee can't be over 100.");
    }

    public static bool BeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public sealed class AcceptManagementRequestValidator : AbstractValidator<AcceptManagementRequestDto>
{
    public AcceptManagementRequestValidator() => AgreementRules.Terms(this);
}

public sealed class CreateAgreementValidator : AbstractValidator<CreateAgreementDto>
{
    public CreateAgreementValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        AgreementRules.Terms(this);
    }
}

public sealed class RenewAgreementValidator : AbstractValidator<RenewAgreementDto>
{
    public RenewAgreementValidator()
    {
        RuleFor(x => x.ManagementFeePercent).InclusiveBetween(0m, 100m).When(x => x.ManagementFeePercent is not null);
        AgreementRules.Fees(this, x => x.LeasingFeeType, x => x.LeasingFeeValue);
        RuleFor(x => x.DocumentUrl).Must(AgreementRules.BeUrl).MaximumLength(500).When(x => x.DocumentUrl is not null)
            .WithMessage("documentUrl must be an http(s) URL.");
    }
}

public sealed class AgreementReasonValidator : AbstractValidator<AgreementReasonDto>
{
    public AgreementReasonValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}
