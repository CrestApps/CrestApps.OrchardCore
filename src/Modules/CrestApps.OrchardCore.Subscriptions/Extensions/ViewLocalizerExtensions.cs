using CrestApps.OrchardCore.Payments.Models;
using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Extensions;

public static class ViewLocalizerExtensions
{
    public static LocalizedHtmlString GetAmount(this IViewLocalizer T, DurationType type, int duration, double amount)
    {
        return type switch
        {
            DurationType.Day => T.Plural(duration, "{1} per day", "{1} per {0} days", amount.ToString("C")),
            DurationType.Week => T.Plural(duration, "{1} per week", "{1} per {0} weeks", amount.ToString("C")),
            DurationType.Month => T.Plural(duration, "{1} per month", "{1} per {0} months", amount.ToString("C")),
            DurationType.Year => T.Plural(duration, "{1} per year", "{1} per {0} years", amount.ToString("C")),
            _ => throw new InvalidOperationException("Duration type is not supported."),
        };
    }
}
