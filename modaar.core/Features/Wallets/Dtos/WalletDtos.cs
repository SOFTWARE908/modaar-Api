using FluentValidation;
using modaar.api.Features.Wallets.Entities;

namespace modaar.api.Features.Wallets.Dtos
{
    public record WalletDto
    {
        public required decimal Balance { get; init; }
        public required string Currency { get; init; }
    }

    // A row in the wallet history, newest first.
    public record WalletTransactionDto
    {
        public required Guid Id { get; init; }
        public required WalletTransactionType Type { get; init; }
        public required WalletTransactionStatus Status { get; init; }
        public required decimal Amount { get; init; }
        public required string Currency { get; init; }
        public decimal? BalanceAfter { get; init; }

        // "mada •••• 1111" for a card top-up.
        public string? PaymentMethod { get; init; }
        public string? FailureReason { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
    }

    public record StartTopUpDto
    {
        public required decimal Amount { get; init; }
    }

    public record WalletTopUpResultDto
    {
        public required Guid TransactionId { get; init; }
        public required WalletTransactionStatus Status { get; init; }

        // The wallet after this top-up (unchanged when it failed).
        public required decimal Balance { get; init; }
        public string? PaymentMethod { get; init; }
        public string? FailureReason { get; init; }
    }
}

namespace modaar.api.Features.Wallets.Validation
{
    using modaar.api.Features.Wallets.Dtos;

    public sealed class StartTopUpValidator : AbstractValidator<StartTopUpDto>
    {
        public StartTopUpValidator()
        {
            RuleFor(x => x.Amount)
                .GreaterThanOrEqualTo(1).LessThanOrEqualTo(50_000)
                .Must(a => decimal.Round(a, 2) == a).WithMessage("At most two decimal places (halalas).");
        }
    }
}
