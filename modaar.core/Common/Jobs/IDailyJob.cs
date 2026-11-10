namespace modaar.api.Common.Jobs;

// One piece of once-a-day work: reminders, status sweeps. Each run gets a fresh DI scope and
// Riyadh's "today", so a job never computes the date itself.
//
// A job must be safe to run more than once on the same day (restart, two runs after a deploy):
// anything it sends goes through SentReminders, which keeps one row per reminder.
public interface IDailyJob
{
    string Name { get; }

    Task RunAsync(DateOnly today, CancellationToken ct);
}

public sealed class JobsSettings
{
    public const string SectionName = "Jobs";

    // Off switch, e.g. for a second server or a local run against shared data.
    public bool Enabled { get; set; } = true;

    // Riyadh hour the daily run starts. Reminders land in the morning, not at midnight.
    public int RunHour { get; set; } = 9;
}
