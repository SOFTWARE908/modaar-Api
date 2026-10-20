using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace modaar.api.Features.Payments.Gateway;

// What we read back from Moyasar about one payment. Only the fields the server checks.
public sealed record GatewayPayment(
    string Id,
    string Status,              // "paid", "failed", "initiated", …
    long AmountHalalas,
    string Currency,
    IReadOnlyDictionary<string, string> Metadata,
    string? CardBrand,          // "mada", "visa", "master"
    string? CardNumber,         // masked, as Moyasar returns it
    string? Message);           // failure reason, e.g. "UNSPECIFIED FAILURE"

// The server never takes the app's word that a card was charged: it fetches the payment from the
// gateway with the secret key and checks status, amount and metadata itself.
public interface IPaymentGatewayClient
{
    // "moyasar" or "simulator" — returned to the app so it knows whether to open the SDK.
    string Provider { get; }

    // Null when the gateway doesn't know the id. `expected` is only used by the simulator,
    // which has no real payment to read.
    Task<GatewayPayment?> GetPaymentAsync(string gatewayPaymentId, ExpectedCharge expected, CancellationToken ct);
}

// What our side asked to be charged: tagged in the metadata under MetadataKey with our attempt id.
// Installments and wallet top-ups use different keys, so one webhook can tell them apart.
public sealed record ExpectedCharge(string MetadataKey, Guid AttemptId, decimal Amount, string Currency)
{
    // For the webhook, which doesn't know yet what the charge was for.
    public static readonly ExpectedCharge Unknown = new(PaymentAmounts.TransactionMetadataKey, Guid.Empty, 0, "SAR");
}

public sealed class MoyasarClient : IPaymentGatewayClient
{
    private readonly HttpClient _http;

    public MoyasarClient(HttpClient http, IOptions<PaymentSettings> settings)
    {
        var moyasar = settings.Value.Moyasar;
        _http = http;
        _http.BaseAddress = new Uri(moyasar.BaseUrl);
        // HTTP Basic: the secret key is the user name, the password is empty.
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{moyasar.SecretKey}:")));
    }

    public string Provider => "moyasar";

    public async Task<GatewayPayment?> GetPaymentAsync(string gatewayPaymentId, ExpectedCharge expected, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"payments/{Uri.EscapeDataString(gatewayPaymentId)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
        var root = doc.RootElement;

        var metadata = new Dictionary<string, string>();
        if (root.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object)
            foreach (var p in meta.EnumerateObject())
                metadata[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText();

        root.TryGetProperty("source", out var source);
        string? Source(string name) =>
            source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        return new GatewayPayment(
            root.GetProperty("id").GetString()!,
            root.GetProperty("status").GetString()!,
            root.GetProperty("amount").GetInt64(),
            root.GetProperty("currency").GetString()!,
            metadata,
            Source("company"),
            Source("number"),
            Source("message"));
    }
}

// Stands in for Moyasar until the keys are set, so the app can build the whole flow now.
// The app skips the SDK and confirms with a made-up id:
//   "sim_paid_<anything>"   → paid with a mada card ending 1111
//   "sim_failed_<anything>" → declined
// Anything else is "not found". Never registered when a secret key is configured.
public sealed class SimulatedMoyasarClient : IPaymentGatewayClient
{
    public string Provider => "simulator";

    public Task<GatewayPayment?> GetPaymentAsync(string gatewayPaymentId, ExpectedCharge expected, CancellationToken ct)
    {
        var status = gatewayPaymentId.StartsWith("sim_paid_", StringComparison.Ordinal) ? "paid"
            : gatewayPaymentId.StartsWith("sim_failed_", StringComparison.Ordinal) ? "failed"
            : null;

        if (status is null)
            return Task.FromResult<GatewayPayment?>(null);

        return Task.FromResult<GatewayPayment?>(new GatewayPayment(
            gatewayPaymentId, status, PaymentAmounts.ToHalalas(expected.Amount), expected.Currency,
            new Dictionary<string, string> { [expected.MetadataKey] = expected.AttemptId.ToString() },
            "mada", "4201-32XX-XXXX-1111", status == "paid" ? "APPROVED" : "UNSPECIFIED FAILURE"));
    }
}

public static class PaymentAmounts
{
    // The metadata key the app must send to Moyasar, so a charge can be tied back to our attempt.
    public const string TransactionMetadataKey = "transaction_id";

    // The key a wallet top-up is tagged with instead.
    public const string WalletMetadataKey = "wallet_transaction_id";

    public static long ToHalalas(decimal sar) => (long)decimal.Round(sar * 100m, 0, MidpointRounding.AwayFromZero);
}

// How a card shows in the app: "mada •••• 4821", "Visa •••• 1111".
public static class CardLabels
{
    public static string Brand(string? company) => company?.ToLowerInvariant() switch
    {
        "mada" => "mada",
        "visa" => "Visa",
        "master" or "mastercard" => "Mastercard",
        null or "" => "",
        var other => other
    };

    public static string? LastFour(string? masked)
    {
        var digits = new string((masked ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? digits[^4..] : null;
    }

    public static string? Display(string? brand, string? last4) =>
        last4 is null ? null : $"{brand} •••• {last4}".Trim();
}
