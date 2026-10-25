using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Jobs;
using modaar.api.Features.Agreements.Entities;
using modaar.api.Features.Agreements.Services;
using modaar.api.Features.Notifications.Entities;
using modaar.api.Features.Notifications.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Agreements.Jobs;

// Lets units whose lease is over leave their agreement (and the agreement expire with its last
// unit). Registered right after ContractExpiryJob, so leases are already marked expired, and
// before the rent and renewal reminders, so those go to whoever runs the unit now.
public sealed class AgreementUnitExitJob : IDailyJob
{
    private readonly IManagementAgreementService _agreements;

    public AgreementUnitExitJob(IManagementAgreementService agreements) => _agreements = agreements;

    public string Name => "agreement-unit-exits";

    public Task RunAsync(DateOnly today, CancellationToken ct) => _agreements.SyncPropertiesAsync(propertyIds: null, ct);
}

// Tells the owner and the broker a unit is about to leave: 30 days out and again 7 days out.
// Windows, not exact days, so a missed run catches up.
public sealed class AgreementExitReminderJob : IDailyJob
{
    private static readonly int[] DaysBefore = [30, 7];

    private readonly ModaarDbContext _db;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly ILogger<AgreementExitReminderJob> _logger;

    public AgreementExitReminderJob(
        ModaarDbContext db, INotificationService notifications, TimeProvider time, ILogger<AgreementExitReminderJob> logger)
    {
        _db = db;
        _notifications = notifications;
        _time = time;
        _logger = logger;
    }

    public string Name => "agreement-exit-reminders";

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        var horizon = today.AddDays(DaysBefore[0]);

        var leaving = await _db.AgreementProperties
            .AsNoTracking()
            .Where(x => x.ActiveFrom != null && x.ExitedOn == null && x.EndsOn != null && x.EndsOn >= today && x.EndsOn <= horizon)
            .Join(_db.ManagementAgreements.Where(a => a.Status == ManagementAgreementStatus.Active),
                x => x.AgreementId, a => a.Id,
                (x, a) => new
                {
                    x.Id,
                    EndsOn = x.EndsOn!.Value,
                    a.Number,
                    AgreementId = a.Id,
                    a.OwnerUserId,
                    BrokerUserId = _db.Brokers.Where(b => b.Id == a.BrokerId).Select(b => b.UserId).FirstOrDefault(),
                    Title = _db.Properties.Where(p => p.Id == x.PropertyId).Select(p => p.Title).FirstOrDefault(),
                    UnitNumber = _db.Properties.Where(p => p.Id == x.PropertyId).Select(p => p.UnitNumber).FirstOrDefault()
                })
            .ToListAsync(ct);

        if (leaving.Count == 0)
            return;

        // The tightest window the unit is inside: 5 days left → the 7-day reminder.
        var due = leaving.ToDictionary(
            x => $"agreement-exit-{DaysBefore.Last(d => x.EndsOn.DayNumber - today.DayNumber <= d)}:{x.Id}",
            x => x);

        var fresh = await SentReminders.ClaimAsync(_db, due.Keys, _time.GetUtcNow(), ct);
        foreach (var key in fresh)
        {
            var x = due[key];
            var title = string.IsNullOrWhiteSpace(x.UnitNumber) ? x.Title ?? string.Empty : $"{x.Title} · {x.UnitNumber}";
            await _notifications.NotifyAsync([x.OwnerUserId, x.BrokerUserId],
                AgreementNotifications.UnitLeaving(x.AgreementId, x.Number, title, x.EndsOn), ct);
        }

        if (fresh.Count > 0)
            _logger.LogInformation("Agreement exit reminders: {Count} unit(s).", fresh.Count);
    }
}
