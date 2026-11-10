using Microsoft.Extensions.Options;
using modaar.api.Common.Time;

namespace modaar.api.Common.Jobs;

// Runs every IDailyJob once a day at JobsSettings.RunHour, Riyadh time.
//
// On startup it also runs straight away if today's hour has already passed: a deploy at noon
// shouldn't skip the day. That is safe because every job is idempotent (see IDailyJob).
//
// One job failing doesn't stop the others; it is logged and tried again tomorrow.
public sealed class DailyJobsRunner : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _time;
    private readonly JobsSettings _settings;
    private readonly ILogger<DailyJobsRunner> _logger;

    public DailyJobsRunner(
        IServiceScopeFactory scopes, TimeProvider time, IOptions<JobsSettings> settings, ILogger<DailyJobsRunner> logger)
    {
        _scopes = scopes;
        _time = time;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Daily jobs are disabled.");
            return;
        }

        if (RiyadhNow().Hour >= _settings.RunHour)
            await RunAllAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = UntilNextRun();
            _logger.LogInformation("Next daily jobs run in {Delay}.", delay);

            try { await Task.Delay(delay, _time, stoppingToken); }
            catch (OperationCanceledException) { return; }

            await RunAllAsync(stoppingToken);
        }
    }

    private async Task RunAllAsync(CancellationToken ct)
    {
        var today = SaudiTime.Today(_time);

        using var scope = _scopes.CreateScope();
        foreach (var job in scope.ServiceProvider.GetServices<IDailyJob>())
        {
            try
            {
                await job.RunAsync(today, ct);
                _logger.LogInformation("Daily job {Job} done for {Day}.", job.Name, today);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogError(e, "Daily job {Job} failed for {Day}.", job.Name, today);
            }
        }
    }

    private DateTime RiyadhNow() => TimeZoneInfo.ConvertTime(_time.GetUtcNow(), SaudiTime.Zone).DateTime;

    private TimeSpan UntilNextRun()
    {
        var now = RiyadhNow();
        var next = now.Date.AddHours(_settings.RunHour);
        if (next <= now)
            next = next.AddDays(1);

        return next - now;
    }
}
