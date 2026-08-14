using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core;

/// <summary>
/// Provides extension methods for <see cref="IViewLocalizer"/> in the omnichannel context.
/// </summary>
public static class ViewLocalizerExtensions
{
    /// <summary>
    /// Converts the specified number to a localized ordinal HTML string (e.g., "1st", "2nd", "3rd").
    /// </summary>
    /// <param name="S">The view localizer instance.</param>
    /// <param name="number">The number to convert to an ordinal representation.</param>
    /// <returns>A <see cref="LocalizedHtmlString"/> containing the ordinal representation of the number.</returns>
    public static LocalizedHtmlString ToOrdinal(this IViewLocalizer S, int number)
    {
        if (number <= 0)
        {
            var numStr = number.ToString();

            return new LocalizedHtmlString(numStr, numStr);
        }

        var lastTwoDigits = number % 100;
        var lastDigit = number % 10;

        var suffix = lastTwoDigits is 11 or 12 or 13
        ? "th"
        : lastDigit switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };

        return S["{0}<sup>{1}</sup> attempt", number, suffix];
    }
}
