using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Localization;
using modaar.api.Persistence;

namespace modaar.api.Common.Push;

// The background worker: runs for the life of the app, takes jobs off PushQueue one at a time,
// looks up the recipients' phones, and sends.
//
// A BackgroundService is a singleton, but DbContext is scoped (one per request). So each job opens
// its own scope and gets a fresh DbContext from it — never hold one across jobs.
public sealed class PushDispatcher : BackgroundService
{
    private readonly PushQueue _queue;
    private readonly IPushSender _sender;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PushDispatcher> _logger;

    public PushDispatcher(PushQueue queue, IPushSender sender, IServiceScopeFactory scopes, ILogger<PushDispatcher> logger)
    {
        _queue = queue;
        _sender = sender;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Push dispatcher started; push is {State}.", _sender.IsEnabled ? "enabled" : "not configured");

        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            // One bad job must not stop the worker for every job after it.
            try
            {
                await DispatchAsync(job, stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogError(e, "Push job for {Count} user(s) failed.", job.UserIds.Count);
            }
        }
    }

    private async Task DispatchAsync(PushJob job, CancellationToken ct)
    {
        if (!_sender.IsEnabled)
            return;

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ModaarDbContext>();

        // Every registered phone of every recipient, with its owner's language and unread count
        // (for the iOS icon badge), in one query.
        var targets = await db.UserDevices
            .AsNoTracking()
            .Where(d => job.UserIds.Contains(d.UserId) && d.FcmToken != null)
            .Select(d => new
            {
                d.FcmToken,
                Language = db.Users.Where(u => u.Id == d.UserId).Select(u => u.PreferredLanguage).FirstOrDefault(),
                Unread = db.Notifications.Count(n => n.UserId == d.UserId && !n.IsRead)
            })
            .ToListAsync(ct);

        if (targets.Count == 0)
            return;

        var messages = targets.Select(t =>
        {
            var english = t.Language == RequestLanguage.English;
            return new PushMessage(
                t.FcmToken!,
                english ? job.TitleEn : job.TitleAr,
                english ? job.BodyEn : job.BodyAr,
                job.Data,
                t.Unread);
        }).ToList();

        var result = await _sender.SendAsync(messages, ct);

        // Forget tokens Firebase says are gone, so the next push doesn't try them again.
        if (result.DeadTokens.Count > 0)
        {
            await db.UserDevices
                .Where(d => d.FcmToken != null && result.DeadTokens.Contains(d.FcmToken))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.FcmToken, (string?)null), ct);

            _logger.LogInformation("Removed {Count} dead push token(s).", result.DeadTokens.Count);
        }
    }
}
