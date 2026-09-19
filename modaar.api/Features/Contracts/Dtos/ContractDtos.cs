using FluentValidation;
using modaar.api.Common.Dtos;
using modaar.api.Features.Users.Dtos;
using modaar.api.Features.Contracts.Enums;

namespace modaar.api.Features.Contracts.Dtos
{
    public record ContractDto
    {
        public required Guid Id { get; init; }
        public required string ContractNumber { get; init; }

        public required string Title { get; init; }

        public required decimal MonthlyAmount { get; init; }
        public required string Currency { get; init; }

        public required DateOnly StartDate { get; init; }
        public required DateOnly EndDate { get; init; }

        // Terminated leases report as Expired to the client, which has only two states. The
        // distinction is kept in the database because ending early and running out are
        // different facts.
        public required ContractStatus Status { get; init; }

        // Null until a signed document has been uploaded for this lease.
        public string? PdfUrl { get; init; }

        public required Guid PropertyId { get; init; }
        public required string PropertyTitle { get; init; }
    }

    public record ContractDetailsDto
    {
        public required ContractDto Contract { get; init; }

        public string? Description { get; init; }
        public required IReadOnlyList<string> ImageUrls { get; init; }

        // The brokerage that was managing the property when the lease was signed.
        public BrokerSummaryDto? Broker { get; init; }

        public required IReadOnlyList<ContractRequestHistoryItemDto> RequestsHistory { get; init; }

        // Computed: Active, inside the renewal window, and nothing already pending.
        public required ContractRenewalAvailability RenewalAvailability { get; init; }
        public required bool CanRenew { get; init; }

        public required int RemainingDays { get; init; }
    }

    public record ContractRequestHistoryItemDto
    {
        public required Guid Id { get; init; }
        public required ContractRequestType Type { get; init; }
        public required ContractRequestStatus Status { get; init; }

        public string? Notes { get; init; }
        public string? DecisionReason { get; init; }

        public required DateTimeOffset Date { get; init; }
        public DateTimeOffset? DecidedAt { get; init; }
    }

    public record ContractListQueryDto : PagedRequestDto
    {
        public ContractStatus? Status { get; init; }
        public Guid? PropertyId { get; init; }
    }

    public record RenewContractRequestDto
    {
        public string? Notes { get; init; }
    }

    public record EndContractRequestDto
    {
        public required string Reason { get; init; }
    }
}

namespace modaar.api.Features.Contracts.Validation
{
    using modaar.api.Features.Contracts.Dtos;

    public sealed class RenewContractRequestValidator : AbstractValidator<RenewContractRequestDto>
    {
        public RenewContractRequestValidator()
        {
            RuleFor(x => x.Notes).MaximumLength(1000);
        }
    }

    public sealed class EndContractRequestValidator : AbstractValidator<EndContractRequestDto>
    {
        public EndContractRequestValidator()
        {
            // Ending a tenancy is consequential and disputed later; require a stated reason.
            RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(1000);
        }
    }
}
