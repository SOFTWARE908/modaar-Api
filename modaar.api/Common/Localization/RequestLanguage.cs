using System.Globalization;
using Microsoft.EntityFrameworkCore;
using modaar.api.Common.Auth;
using modaar.api.Persistence;

namespace modaar.api.Common.Localization;

// Resolution order: Accept-Language, then the user's stored preference, then Arabic.
//
// The header wins because it reflects what the person is looking at right now — someone who
// switched their phone to English expects English, even if their profile still says Arabic.
// The stored preference is the fallback for requests with no usable header, and it is what a
// push notification or a scheduled email would use, where there is no request at all.
//
// Scoped, and resolved once per request: the user lookup must not run per field.
public sealed class RequestLanguage : IRequestLanguage
{
    public const string Arabic = "ar";
    public const string English = "en";

    private readonly Lazy<string> _current;

    public RequestLanguage(IHttpContextAccessor accessor, ModaarDbContext db)
    {
        _current = new Lazy<string>(() => Resolve(accessor, db));
    }

    public string Current => _current.Value;

    public bool IsArabic => Current == Arabic;

    public string Pick(string? arabic, string? english)
    {
        var preferred = IsArabic ? arabic : english;

        // Fall back to the other language rather than returning nothing: a broker whose English
        // subtitle was never filled in should still show their Arabic one.
        return !string.IsNullOrWhiteSpace(preferred)
            ? preferred
            : (IsArabic ? english : arabic) ?? string.Empty;
    }

    // The culture to format dates and numbers with, so month names in a visit summary match the
    // rest of the response.
    public CultureInfo Culture => CultureInfo.GetCultureInfo(IsArabic ? "ar-SA" : "en-US");

    private static string Resolve(IHttpContextAccessor accessor, ModaarDbContext db)
    {
        var context = accessor.HttpContext;
        if (context is null)
            return Arabic;

        var header = context.Request.Headers.AcceptLanguage.ToString();
        if (!string.IsNullOrWhiteSpace(header))
        {
            // Takes the first tag and its primary subtag: "en-GB,ar;q=0.8" → "en".
            var tag = header.Split(',')[0].Split(';')[0].Split('-')[0].Trim().ToLowerInvariant();

            if (tag is Arabic or English)
                return tag;
        }

        if (context.User.GetUserId() is { } userId)
        {
            // Blocking on purpose: the interface is synchronous because it is called from inside
            // projections, and the value is resolved at most once per request.
            var stored = db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.PreferredLanguage)
                .FirstOrDefault();

            if (stored is Arabic or English)
                return stored;
        }

        return Arabic;
    }
}
