using CrestApps.OrchardCore.Payments.Models;
using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Extensions;

/// <summary>
/// Provides subscription formatting helpers for Razor view localization.
/// </summary>
public static class ViewLocalizerExtensions
{
    /// <summary>
    /// Formats a localized recurring amount label for the specified billing duration.
    /// </summary>
    /// <param name="T">The view localizer used to create the localized HTML string.</param>
    /// <param name="type">The billing duration type.</param>
    /// <param name="duration">The number of duration units in the billing period.</param>
    /// <param name="amount">The recurring amount to format as currency.</param>
    /// <param name="currency">The ISO-4217 code of the currency the amount is in.</param>
    /// <returns>A localized HTML string that describes the recurring amount.</returns>
    public static LocalizedHtmlString GetAmount(this IViewLocalizer T, DurationType type, int duration, decimal amount, string currency)
    {
        var price = PriceFormatting.Format(amount, currency);

        return type switch
        {
            DurationType.Day => T.Plural(duration, "{1} per day", "{1} per {0} days", price),
            DurationType.Week => T.Plural(duration, "{1} per week", "{1} per {0} weeks", price),
            DurationType.Month => T.Plural(duration, "{1} per month", "{1} per {0} months", price),
            DurationType.Year => T.Plural(duration, "{1} per year", "{1} per {0} years", price),
            _ => throw new InvalidOperationException("Duration type is not supported."),
        };
    }
}
