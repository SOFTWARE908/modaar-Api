using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace modaar.api.Common.Push;

// Sends through Firebase Cloud Messaging (HTTP v1). FCM delivers to Android directly and to iOS
// through APNs, using the APNs key uploaded to the Firebase project.
public sealed class FirebasePushSender : IPushSender
{
    // FCM's limit for one SendEach call.
    private const int BatchSize = 500;

    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushSender> _logger;

    public FirebasePushSender(string credentialsPath, ILogger<FirebasePushSender> logger)
    {
        _logger = logger;

        // One FirebaseApp per process. Named, so it can't clash with a default app anything else
        // might create.
        // CredentialFactory with an explicit type: the file must be a service-account key, so a
        // different credential type dropped at that path can't be loaded by mistake.
        var app = FirebaseApp.GetInstance("modaar") ?? FirebaseApp.Create(new AppOptions
        {
            Credential = CredentialFactory.FromFile<ServiceAccountCredential>(credentialsPath).ToGoogleCredential()
        }, "modaar");

        _messaging = FirebaseMessaging.GetMessaging(app);
    }

    public bool IsEnabled => true;

    public async Task<PushResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        var dead = new List<string>();

        foreach (var batch in messages.Chunk(BatchSize))
        {
            var response = await _messaging.SendEachAsync(batch.Select(ToFirebase).ToList(), ct);

            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                if (r.IsSuccess)
                    continue;

                // Unregistered = the app was removed or the token rotated: forget it. Anything else
                // (quota, network, a bad payload) is a failure of this send, not of the token.
                if (r.Exception?.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.SenderIdMismatch)
                    dead.Add(batch[i].Token);
                else
                    _logger.LogWarning(r.Exception, "Push to one device failed.");
            }

            _logger.LogInformation("Push batch: {Sent} sent, {Failed} failed.", response.SuccessCount, response.FailureCount);
        }

        return new PushResult(dead);
    }

    private static Message ToFirebase(PushMessage m) => new()
    {
        // The SDK marks Token obsolete in favour of Fid, but Fid is a different identifier (the
        // Firebase Installation ID). The app registers what FirebaseMessaging.getToken() returns,
        // which is a registration token, and that still goes here.
#pragma warning disable CS0618
        Token = m.Token,
#pragma warning restore CS0618
        // The visible part: shown by the phone itself when the app is closed.
        Notification = new Notification { Title = m.Title, Body = m.Body },
        // The invisible part: read by the app when the user taps, to open the right screen.
        Data = m.Data,
        Android = new AndroidConfig { Priority = Priority.High },
        // The number on the iOS app icon.
        Apns = m.Badge is { } badge ? new ApnsConfig { Aps = new Aps { Badge = badge } } : null
    };
}

// Used while Firebase isn't configured. Notifications still save and still reach open apps over
// SignalR; only the push to closed apps is skipped.
public sealed class NullPushSender : IPushSender
{
    private readonly ILogger<NullPushSender> _logger;

    public NullPushSender(ILogger<NullPushSender> logger) => _logger = logger;

    public bool IsEnabled => false;

    public Task<PushResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct)
    {
        _logger.LogDebug("Push is not configured; skipped {Count} message(s).", messages.Count);
        return Task.FromResult(PushResult.None);
    }
}
