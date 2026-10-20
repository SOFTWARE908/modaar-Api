using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Jobs;
using modaar.api.Features.Notifications.Entities;
using modaar.api.Features.Notifications.Services;
using modaar.api.Features.Payments.Enums;
using modaar.api.Features.Users.Services;
using modaar.api.Persistence;

namespace modaar.api.Features.Payments.Jobs;

// Daily reminders for unpaid rent installments:
//   due within the next 7 days → the tenant, once
//   due today                  → the tenant
//   overdue                    → the tenant and whoever runs the property (owner or broker),
//                                the first day late and then once a week until it is paid
//
// Each reminder is a window, not an exact day, so a missed run catches up the next morning
// (SentReminders stops it going twice). An installment that arrives 3 days before its due date
// still gets the "due soon" reminder.
public sealed class RentReminderJob : IDailyJob
{
    private const int UpcomingDays = 7;
    private const int OverdueEveryDays = 7;

    private readonly ModaarDbContext _db;
    private readonly INotificationService _notifications;
    private readonly TimeProvider _time;
    private readonly ILogger<RentReminderJob> _logger;

    public RentReminderJob(
        ModaarDbContext db, INotificationService notifications, TimeProvider time, ILogger<RentReminderJob> logger)
    {
        _db = db;
        _notifications = notifications;
        _time = time;
        _logger = logger;
    }

    public string Name => "rent-reminders";

    public async Task RunAsync(DateOnly today, CancellationToken ct)
    {
        var horizon = today.AddDays(UpcomingDays);

        var installments = await _db.Payments
            .AsNoTracking()
            .Where(p => p.Type == PaymentType.Rent && p.Status != PaymentStatus.Paid
                     && p.DueDate != null && p.DueDate <= horizon)
            .Select(p => new
            {
                p.Id,
                p.Amount,
                DueDate = p.DueDate!.Value,
                p.PayerUserId,
                PropertyId = p.PropertyId ?? _db.Contracts.Where(c => c.Id == p.ContractId).Select(c => (Guid?)c.PropertyId).FirstOrDefault(),
                TenantName = _db.Users.Where(u => u.Id == p.PayerUserId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(ct);

        if (installments.Count == 0)
            return;

        // Which reminder each installment is due for today, by its key.
        var due = installments.ToDictionary(p => KeyFor(p.Id, p.DueDate, today), p => p);
        var fresh = await SentReminders.ClaimAsync(_db, due.Keys, _time.GetUtcNow(), ct);
        if (fresh.Count == 0)
            return;

        var propertyIds = due.Where(d => fresh.Contains(d.Key) && d.Value.PropertyId is not null)
            .Select(d => d.Value.PropertyId!.Value).Distinct().ToList();

        var titles = await _db.Properties.AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Title, ct);

        var handlers = await PropertyHandlers.ResolveAsync(_db, propertyIds, ct);

        foreach (var key in fresh)
        {
            var p = due[key];
            var subject = new PaymentNotifications.Subject(
                p.Id, p.PropertyId is { } pid && titles.TryGetValue(pid, out var t) ? t : string.Empty,
                p.Amount, p.DueDate, p.TenantName);

            if (p.DueDate > today)
                await _notifications.NotifyAsync([p.PayerUserId], PaymentNotifications.Upcoming(subject), ct);
            else if (p.DueDate == today)
                await _notifications.NotifyAsync([p.PayerUserId], PaymentNotifications.DueToday(subject), ct);
            else
            {
                await _notifications.NotifyAsync([p.PayerUserId], PaymentNotifications.OverdueToTenant(subject), ct);
                if (p.PropertyId is { } propertyId && handlers.TryGetValue(propertyId, out var handler))
                    await _notifications.NotifyAsync([handler], PaymentNotifications.OverdueToHandler(subject), ct);
            }
        }

        _logger.LogInformation("Rent reminders: {Count} sent.", fresh.Count);
    }

    // Overdue keys carry the week number (day 1–7 late = week 0, 8–14 = week 1…), so there is
    // one reminder per week of lateness and a missed day still lands in the right week.
    private static string KeyFor(Guid paymentId, DateOnly dueDate, DateOnly today)
    {
        if (dueDate > today) return $"rent-upcoming:{paymentId}";
        if (dueDate == today) return $"rent-due:{paymentId}";

        var daysLate = today.DayNumber - dueDate.DayNumber;
        return $"rent-overdue:{paymentId}:{(daysLate - 1) / OverdueEveryDays}";
    }
}
