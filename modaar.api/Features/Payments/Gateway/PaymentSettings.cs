namespace modaar.api.Features.Payments.Gateway;

// "Payments" section of appsettings. Keys live in config (or env vars on the server), never in
// code: going from test to live is swapping sk_test_/pk_test_ for sk_live_/pk_live_.
public sealed class PaymentSettings
{
    public const string SectionName = "Payments";

    public MoyasarSettings Moyasar { get; set; } = new();
    public BankTransferSettings BankTransfer { get; set; } = new();
}

public sealed class MoyasarSettings
{
    // Empty → the built-in simulator answers instead of Moyasar (see SimulatedMoyasarClient).
    public string? SecretKey { get; set; }

    // Given to the app for the Moyasar SDK. Safe to share: it can only create payments.
    public string? PublishableKey { get; set; }

    // The "secret token" set on the webhook in the Moyasar dashboard. Empty → webhook disabled.
    public string? WebhookSecret { get; set; }

    public string BaseUrl { get; set; } = "https://api.moyasar.com/v1/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);
}

// The platform's account the tenant transfers rent to.
public sealed class BankTransferSettings
{
    public string? BankName { get; set; }
    public string? AccountName { get; set; }
    public string? Iban { get; set; }
}
