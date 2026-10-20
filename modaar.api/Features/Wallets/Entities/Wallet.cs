using System.Text.Json.Serialization;

namespace modaar.api.Features.Wallets.Entities;

// An owner's balance on the platform. Today it only goes up, by card top-ups; rent coming in,
// withdrawals and maintenance deductions come later and will each be a WalletTransaction type.
// Created on first use, one per user.
public class Wallet
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    // Only ever changed together with a completed WalletTransaction, inside one database
    // transaction, so the history always adds up to it.
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "SAR";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

// One movement on a wallet. A top-up starts Initiated (card checkout created) and becomes
// Completed (balance credited) or Failed. Amount is positive for money in.
public class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid WalletId { get; set; }

    public WalletTransactionType Type { get; set; }
    public WalletTransactionStatus Status { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "SAR";

    // Balance right after this movement; null until it completes.
    public decimal? BalanceAfter { get; set; }

    // Card top-up: Moyasar's payment id (unique, so one charge credits once) and the card shown.
    public string? GatewayPaymentId { get; set; }
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WalletTransactionType
{
    TopUp
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WalletTransactionStatus
{
    Initiated,
    Completed,
    Failed
}
