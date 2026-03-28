using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core;

public static class ViewLocalizerExtensions
{
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
