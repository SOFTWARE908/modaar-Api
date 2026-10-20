using FluentValidation;
using modaar.api.Features.Payments.Entities;

namespace modaar.api.Features.Payments.Dtos
{
    // What the app passes to the Moyasar SDK. Amount is final: the server works it out
    // (installment + service fee + VAT) and checks the charge against it afterwards.
    public record CardCheckoutDto
    {
        public required Guid TransactionId { get; init; }

        // "moyasar" → open the Moyasar SDK with PublishableKey.
        // "simulator" → no SDK; confirm straight away with gatewayPaymentId "sim_paid_…" or "sim_failed_…".
        public required string Provider { get; init; }
        public string? PublishableKey { get; init; }

        public required decimal Amount { get; init; }
        // The same amount in halalas, which is what the SDK takes.
        public required long AmountHalalas { get; init; }
        public required string Currency { get; init; }
        public required string Description { get; init; }

        // Send exactly this to the SDK as metadata, or the confirm step will refuse the charge.
        public required IReadOnlyDictionary<string, string> Metadata { get; init; }

        // Card networks to allow in the SDK.
        public required IReadOnlyList<string> SupportedNetworks { get; init; }
    }

    public record ConfirmCardPaymentDto
    {
        public required Guid TransactionId { get; init; }

        // The payment id the Moyasar SDK returned.
        public required string GatewayPaymentId { get; init; }
    }

    public record PaymentResultDto
    {
        public required Guid PaymentId { get; init; }
        public required PaymentTransactionStatus Status { get; init; }

        // Set when paid: "mada •••• 1111" and the receipt number for the success screen.
        public string? PaymentMethod { get; init; }
        public string? ReceiptNumber { get; init; }

        // Set when declined, e.g. "UNSPECIFIED FAILURE".
        public string? FailureReason { get; init; }
    }

    // Where to send a bank transfer: the platform's account. Fields are null until configured.
    public record BankTransferInfoDto
    {
        public string? BankName { get; init; }
        public string? AccountName { get; init; }
        public string? Iban { get; init; }
    }

    // The latest bank transfer on a payment, on the invoice screen.
    public record BankTransferDto
    {
        public required Guid TransactionId { get; init; }
        public required PaymentTransactionStatus Status { get; init; }
        public required string ReceiptUrl { get; init; }
        public required DateTimeOffset SubmittedAt { get; init; }
        public string? RejectionReason { get; init; }
    }

    public record RejectBankTransferDto
    {
        public required string Reason { get; init; }
    }
}

namespace modaar.api.Features.Payments.Validation
{
    using modaar.api.Features.Payments.Dtos;

    public sealed class ConfirmCardPaymentValidator : AbstractValidator<ConfirmCardPaymentDto>
    {
        public ConfirmCardPaymentValidator()
        {
            RuleFor(x => x.TransactionId).NotEmpty();
            RuleFor(x => x.GatewayPaymentId).NotEmpty().MaximumLength(100);
        }
    }

    public sealed class RejectBankTransferValidator : AbstractValidator<RejectBankTransferDto>
    {
        public RejectBankTransferValidator()
        {
            // The tenant has to understand what to fix, so a real reason.
            RuleFor(x => x.Reason).NotEmpty().MinimumLength(3).MaximumLength(500);
        }
    }
}
