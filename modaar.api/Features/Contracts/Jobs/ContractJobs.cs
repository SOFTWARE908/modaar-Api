using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Jobs;
using modaar.api.Features.Contracts.Enums;
using modaar.api.Features.Notifications.Entities;
using modaar.api.Features.Notifications.Services;
using modaar.api.Features.Users.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Contracts.Jobs;

// Moves leases past their end date from Active to Expired. Until now nothing did, so an ended
// lease still let the tenant raise maintenance and still showed on their home screen.
// Registered before the renewal job so that never reminds about a lease that just ended.
public sealed class ContractExpiryJob : IDailyJob
{
    private readonly ModaarDbContext _db;
    private readonly TimeProvider _time;
    private readonly ILogger<ContractExpiryJob> _logger;

    public ContractExpiryJob(ModaarDbContext db, TimeProvider time, ILogger<ContractExpiryJob> logger)
    {
        _db = db;
        _time = time;
        _logger = logger;
    }

    public string Name => "contract-expiry";

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        // EndDate is the last day of the lease, so it expires the day after.
        var expired = await _db.Contracts
            .Where(c => c.Status == ContractStatus.Active && c.EndDate < today)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, ContractStatus.Expired)
                .SetProperty(c => c.UpdatedAt, _time.GetUtcNow()), ct);

        if (expired > 0)
            _logger.LogInformation("Contract expiry: {Count} lease(s) marked expired.", expired);
    }
}

// Reminds both sides that an active lease is ending: 60 days out and again 30 days out, to the
// tenant and to whoever runs the property (owner or broker). Windows, not exact days, so a
// missed run catches up; a lease created with 20 days left gets only the 30-day reminder.
public sealed class ContractRenewalReminderJob : IDailyJob
{
    private static readonly int[] DaysBefore = [60, 30];

    private readonly ModaarDbContext _db;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly ILogger<ContractRenewalReminderJob> _logger;

    public ContractRenewalReminderJob(
        ModaarDbContext db, INotificationService notifications, TimeProvider time, ILogger<ContractRenewalReminderJob> logger)
    {
        _db = db;
        _notifications = notifications;
        _time = time;
        _logger = logger;
    }

    public string Name => "contract-renewal-reminders";

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        var horizon = today.AddDays(DaysBefore[0]);

        var leases = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.Status == ContractStatus.Active && c.EndDate >= today && c.EndDate <= horizon)
            .Select(c => new
            {
                c.Id,
                c.PropertyId,
                c.TenantUserId,
                c.EndDate,
                PropertyTitle = _db.Properties.Where(p => p.Id == c.PropertyId).Select(p => p.Title).FirstOrDefault(),
                TenantName = _db.Users.Where(u => u.Id == c.TenantUserId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(ct);

        if (leases.Count == 0)
            return;

        // The tightest window the lease is inside: 25 days left → the 30-day reminder.
        var due = leases.ToDictionary(
            c => $"renewal-{DaysBefore.Last(d => c.EndDate.DayNumber - today.DayNumber <= d)}:{c.Id}",
            c => c);

        var fresh = await SentReminders.ClaimAsync(_db, due.Keys, _time.GetUtcNow(), ct);
        if (fresh.Count == 0)
            return;

        var handlers = await PropertyHandlers.ResolveAsync(
            _db, due.Where(d => fresh.Contains(d.Key)).Select(d => d.Value.PropertyId).Distinct().ToList(), ct);

        foreach (var key in fresh)
        {
            var c = due[key];
            var subject = new ContractNotifications.Subject(c.Id, c.PropertyTitle ?? string.Empty, c.EndDate, c.TenantName);

            await _notifications.NotifyAsync([c.TenantUserId], ContractNotifications.RenewalToTenant(subject), ct);
            if (handlers.TryGetValue(c.PropertyId, out var handler))
                await _notifications.NotifyAsync([handler], ContractNotifications.RenewalToHandler(subject), ct);
        }

        _logger.LogInformation("Renewal reminders: {Count} lease(s).", fresh.Count);
    }
}
