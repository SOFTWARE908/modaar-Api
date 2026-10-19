using System.Threading.Channels;

namespace modaar.api.Common.Push;

// One notification to push to some users, in both languages: each phone gets its owner's.
public sealed record PushJob(
    IReadOnlyList<Guid> UserIds,
    string TitleAr, string TitleEn,
    string BodyAr, string BodyEn,
    IReadOnlyDictionary<string, string> Data);

// A queue in memory between the request that raises a notification and the background worker
// that pushes it. The request drops the job in and returns at once; Firebase's speed (or
// outage) never reaches the API's response time.
//
// In memory means a restart loses jobs not yet sent. Acceptable here: the notification itself is
// already saved and shows in the app; only the phone banner for it is missed.
public sealed class PushQueue
{
    private readonly Channel<PushJob> _channel = Channel.CreateBounded<PushJob>(new BoundedChannelOptions(10_000)
    {
        // If Firebase is down long enough to fill the queue, drop new pushes rather than let the
        // API slow down or run out of memory waiting.
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    public bool TryEnqueue(PushJob job) => _channel.Writer.TryWrite(job);

    public IAsyncEnumerable<PushJob> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
