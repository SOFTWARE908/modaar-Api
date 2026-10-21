namespace modaar.api.Features.Payments.Entities;

// A public link to one paid receipt ("مشاركة الإيصال"), for sending to someone without the app.
// The token is the only key: 32 random bytes, so it can't be guessed or enumerated. It expires.
public class ReceiptShareLink
{
    public string Token { get; set; } = null!;
    public Guid PaymentId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
