using System.Globalization;

namespace modaar.api.Common.Localization;

// One place that answers "which language is this response in". Everything that picks between an
// _Ar and an _En column goes through it, so the answer can never differ between two screens.
public interface IRequestLanguage
{
    // "ar" or "en".
    string Current { get; }

    bool IsArabic { get; }

    CultureInfo Culture { get; }
    // Picks between a paired Arabic/English value.
    string Pick(string? arabic, string? english);
}
