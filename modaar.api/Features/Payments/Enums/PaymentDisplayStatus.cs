using System.Text.Json.Serialization;

namespace modaar.api.Features.Payments.Enums;

// How an installment reads in the app. For unpaid ones (P-7):
//   overdue   — its due date has passed
//   due       — the next one to pay (no earlier unpaid one on the same lease) and due within
//               PaymentDisplay.DueWindowDays days
//   scheduled — any other unpaid one: further off, or queued behind an earlier one
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentDisplayStatus
{
    Paid,
    Due,
    Overdue,
    Scheduled,
    // A bank-transfer receipt is waiting for the owner or broker to confirm it.
    UnderReview
}

public static class PaymentDisplay
{
    // How close a due date must be for the next installment to read "due" rather than "scheduled".
    // Matches the rent reminder, which goes out 7 days before.
    public const int DueWindowDays = 7;
}
