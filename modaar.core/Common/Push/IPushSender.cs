namespace modaar.api.Common.Push;

// One push to one phone. Title and body are already in that phone owner's language.
public sealed record PushMessage(
    string Token,
    string Title,
    string Body,
    IReadOnlyDictionary<string, string> Data,
    int? Badge);

// Tokens Firebase says no longer exist (app uninstalled, token rotated). The caller forgets them.
public sealed record PushResult(IReadOnlyList<string> DeadTokens)
{
    public static readonly PushResult None = new([]);
}

// Hands pushes to whatever delivers them. Firebase in production; a no-op until Firebase is
// configured, so nothing else in the app has to know whether push is switched on.
public interface IPushSender
{
    bool IsEnabled { get; }

    Task<PushResult> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken ct);
}
