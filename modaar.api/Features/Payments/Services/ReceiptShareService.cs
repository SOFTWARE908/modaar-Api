using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Results;
using modaar.api.Features.Authentication.Enums;
using modaar.api.Features.Payments.Entities;
using modaar.api.Features.Payments.Enums;
using modaar.api.Persistence;

namespace modaar.api.Features.Payments.Services;

public record ReceiptShareLinkDto
{
    public required string Url { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public interface IReceiptShareService
{
    Task<Result<ReceiptShareLinkDto>> CreateAsync(Guid paymentId, Guid callerUserId, AccountType role, string baseUrl, CancellationToken ct);

    // The public page's HTML, or null when the token is unknown or expired.
    Task<string?> RenderAsync(string token, CancellationToken ct);
}

// "مشاركة الإيصال": a public, expiring link to one paid receipt. Anyone who may see the payment in
// the app (its tenant, owner or broker) can make one. The page shows the receipt only — no phone
// numbers, no ids, nothing that leads back into the account.
public sealed class ReceiptShareService : IReceiptShareService
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly ModaarDbContext _db;
    private readonly IPaymentService _payments;
    private readonly TimeProvider _time;

    public ReceiptShareService(ModaarDbContext db, IPaymentService payments, TimeProvider time)
    {
        _db = db;
        _payments = payments;
        _time = time;
    }

    public async Task<Result<ReceiptShareLinkDto>> CreateAsync(
        Guid paymentId, Guid callerUserId, AccountType role, string baseUrl, CancellationToken ct)
    {
        // Same access rule as opening the receipt in the app.
        var invoice = await _payments.GetInvoiceAsync(paymentId, callerUserId, role, ct);
        if (!invoice.Success)
            return Result<ReceiptShareLinkDto>.Fail(invoice.ErrorCode!.Value, invoice.Error!);

        if (invoice.Value!.Status != PaymentStatus.Paid)
            return Result<ReceiptShareLinkDto>.Fail(AppErrorCode.Conflict, "Only a paid payment has a receipt to share.");

        var now = _time.GetUtcNow();

        // Sharing twice gives the same link while it is still valid.
        var link = await _db.ReceiptShareLinks.AsNoTracking()
            .Where(l => l.PaymentId == paymentId && l.ExpiresAt > now)
            .OrderByDescending(l => l.ExpiresAt)
            .FirstOrDefaultAsync(ct);

        if (link is null)
        {
            link = new ReceiptShareLink
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_'),
                PaymentId = paymentId,
                CreatedByUserId = callerUserId,
                CreatedAt = now,
                ExpiresAt = now + Lifetime
            };
            _db.ReceiptShareLinks.Add(link);
            await _db.SaveChangesAsync(ct);
        }

        return Result<ReceiptShareLinkDto>.Ok(new ReceiptShareLinkDto
        {
            Url = $"{baseUrl.TrimEnd('/')}/r/{link.Token}",
            ExpiresAt = link.ExpiresAt
        });
    }

    public async Task<string?> RenderAsync(string token, CancellationToken ct)
    {
        var now = _time.GetUtcNow();

        var row = await _db.ReceiptShareLinks.AsNoTracking()
            .Where(l => l.Token == token && l.ExpiresAt > now)
            .Join(_db.Payments, l => l.PaymentId, p => p.Id, (l, p) => p)
            .Where(p => p.Status == PaymentStatus.Paid)
            .Select(p => new
            {
                p.Title,
                p.Amount,
                p.ServiceFee,
                p.VatAmount,
                p.Currency,
                p.Date,
                p.PeriodStart,
                p.PaymentMethod,
                p.ReceiptNumber,
                TenantName = _db.Users.Where(u => u.Id == p.PayerUserId).Select(u => u.FullName).FirstOrDefault(),
                Unit = _db.Properties.Where(x => x.Id == p.PropertyId).Select(x => new { x.Title, x.UnitNumber }).FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        // Gregorian Arabic (ar-EG), not ar-SA's Hijri.
        var ar = CultureInfo.GetCultureInfo("ar-EG");
        var total = row.Amount + (row.ServiceFee ?? 0) + (row.VatAmount ?? 0);
        var unit = row.Unit is null ? null
            : string.IsNullOrWhiteSpace(row.Unit.UnitNumber) ? row.Unit.Title : $"{row.Unit.Title} · {row.Unit.UnitNumber}";
        var paidAt = TimeZoneInfo.ConvertTime(row.Date, Common.Time.SaudiTime.Zone);

        var lines = new List<(string Label, string? Value)>
        {
            ("رقم الإيصال", row.ReceiptNumber),
            ("البيان", row.Title),
            ("الفترة", row.PeriodStart?.ToString("MMMM yyyy", ar)),
            ("المستأجر", row.TenantName),
            ("الوحدة", unit),
            ("تاريخ الدفع", paidAt.ToString("d MMMM yyyy", ar)),
            ("طريقة الدفع", row.PaymentMethod),
            ("المبلغ", $"{row.Amount:N2} {row.Currency}"),
            ("رسوم الخدمة", row.ServiceFee is { } fee ? $"{fee:N2} {row.Currency}" : null),
            ("ضريبة القيمة المضافة", row.VatAmount is { } vat ? $"{vat:N2} {row.Currency}" : null)
        };

        var rows = string.Concat(lines
            .Where(l => !string.IsNullOrWhiteSpace(l.Value))
            .Select(l => $"<tr><th>{E(l.Label)}</th><td>{E(l.Value)}</td></tr>"));

        return $$"""
            <!doctype html>
            <html lang="ar" dir="rtl">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex">
            <title>إيصال دفع · مُدارة</title>
            <style>
              body { font-family: system-ui, -apple-system, "Segoe UI", Tahoma, sans-serif; background: #f4f6f8; color: #1d2a33; margin: 0; padding: 24px 16px; }
              .card { max-width: 480px; margin: 0 auto; background: #fff; border-radius: 16px; padding: 24px; box-shadow: 0 2px 12px rgba(0,0,0,.06); }
              h1 { font-size: 18px; margin: 0 0 4px; }
              .ok { color: #1b7f5a; font-weight: 600; margin: 0 0 20px; }
              .total { font-size: 28px; font-weight: 700; margin: 0 0 20px; }
              table { width: 100%; border-collapse: collapse; }
              th, td { text-align: start; padding: 10px 0; border-bottom: 1px solid #eef1f4; font-size: 14px; }
              th { color: #6b7a86; font-weight: 500; width: 45%; }
              footer { text-align: center; color: #8a97a1; font-size: 12px; margin-top: 16px; }
            </style>
            </head>
            <body>
              <div class="card">
                <h1>إيصال دفع</h1>
                <p class="ok">تمت عملية الدفع بنجاح</p>
                <p class="total">{{E($"{total:N2} {row.Currency}")}}</p>
                <table>{{rows}}</table>
              </div>
              <footer>مُدارة</footer>
            </body>
            </html>
            """;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
