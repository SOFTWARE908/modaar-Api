using System.Text.Json.Serialization;

namespace modaar.api.Features.Payments.Entities;

// One attempt to settle a Payment: a card charge through Moyasar, or a bank-transfer receipt.
// A payment can have many (a declined card, then a transfer); at most one ends Paid. Kept apart
// from Payment so the installment row stays what was owed, and this is how it got paid.
public class PaymentTransaction
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }

    // Who started it — the tenant paying.
    public Guid PayerUserId { get; set; }

    public PaymentChannel Channel { get; set; }
    public PaymentTransactionStatus Status { get; set; }

    // What was asked for, in SAR, frozen at the start so a later change to the installment can't
    // make an old charge look like the wrong amount.
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";

    // Card: Moyasar's payment id, and what the app shows ("mada", "4821"). The gateway id is
    // unique so one charge can never settle two installments.
    public string? GatewayPaymentId { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public string? FailureReason { get; set; }

    // Bank transfer: the receipt the tenant uploaded, and the owner's or broker's decision.
    public string? ReceiptUrl { get; set; }
    public string? ReceiptFileName { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentChannel
{
    // mada, Visa, Mastercard — through Moyasar.
    Card,
    BankTransfer
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentTransactionStatus
{
    // Card: checkout created, waiting for the app to finish with Moyasar.
    Initiated,
    Paid,
    Failed,
    // Transfer: receipt uploaded, waiting for the owner or broker.
    UnderReview,
    Rejected
}
