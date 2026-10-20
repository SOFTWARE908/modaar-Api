using Microsoft.EntityFrameworkCore;
using modaar.api.Persistence;

namespace modaar.api.Features.Notifications.Entities;

// One row per scheduled reminder that went out, e.g. "rent-upcoming:{paymentId}" or
// "renewal-60:{contractId}". The unique key is what lets the daily jobs run twice in a day, or
// catch up after a missed day, without sending anything twice.
public class SentReminder
{
    public string Key { get; set; } = null!;
    public DateTimeOffset SentAt { get; set; }
}

public static class SentReminders
{
    // Of the candidate keys, records the ones not sent before and returns them. Call before
    // notifying: a crash between the two loses a reminder rather than sending it twice.
    public static async Task<HashSet<string>> ClaimAsync(
        ModaarDbContext db, IReadOnlyCollection<string> keys, DateTimeOffset now, CancellationToken ct)
    {
        if (keys.Count == 0)
            return [];

        var already = await db.SentReminders
            .Where(r => keys.Contains(r.Key))
            .Select(r => r.Key)
            .ToListAsync(ct);

        var fresh = keys.Except(already).ToHashSet();
        if (fresh.Count == 0)
            return fresh;

        db.SentReminders.AddRange(fresh.Select(k => new SentReminder { Key = k, SentAt = now }));
        await db.SaveChangesAsync(ct);

        return fresh;
    }
}
