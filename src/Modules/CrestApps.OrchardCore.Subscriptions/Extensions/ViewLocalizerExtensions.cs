using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Extensions;

public static class ViewLocalizerExtensions
{
    public static LocalizedHtmlString GetAmount(this IViewLocalizer T, BillingDurationType type, int duration, double amount)
    {
        return type switch
        {
            BillingDurationType.Day => T.Plural(duration, "{0} per day", "{1} per {2} days", amount.ToString("C"), duration),
            BillingDurationType.Week => T.Plural(duration, "{0} per week", "{1} per {2} weeks", amount.ToString("C"), duration),
            BillingDurationType.Month => T.Plural(duration, "{0} per month", "{1} per {2} months", amount.ToString("C"), duration),
            BillingDurationType.Year => T.Plural(duration, "{0} per year", "{1} per {2} years", amount.ToString("C"), duration),
            _ => throw new InvalidOperationException("Duration type is not supported."),
        };
    }
}
