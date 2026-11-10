namespace modaar.api.Common.Time;

// Lease progress, remaining days and overdue flags all need a "today", and the app's users are
// on UTC+3. Computing any of them in UTC reports the wrong day for the three hours after
// midnight in Riyadh, so everything date-shaped goes through here.
public static class SaudiTime
{
    public static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateOnly Today(TimeProvider time) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime);

    // Saudi Arabia has no DST, so this is a fixed +03:00 and the fallback is exact rather than
    // an approximation.
    private static TimeZoneInfo ResolveZone()
    {
        foreach (var id in new[] { "Asia/Riyadh", "Arab Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Riyadh", TimeSpan.FromHours(3), "Riyadh", "Riyadh");
    }
}
