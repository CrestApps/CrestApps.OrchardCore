using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core;

public class StripeLimits
{
    public static readonly Dictionary<string, StripePaymentLimits> StripePaymentLimits = new()
    {
        { "AED", new StripePaymentLimits { Minimum = 2.00, Maximum = 999999.99 } },
        { "AUD", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "BRL", new StripePaymentLimits { Minimum = 0.50, Maximum = 50000.00 } },
        { "CAD", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "CHF", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "CNY", new StripePaymentLimits { Minimum = 0.01, Maximum = 999999.99 } },
        { "CZK", new StripePaymentLimits { Minimum = 15.00, Maximum = 999999.99 } },
        { "DKK", new StripePaymentLimits { Minimum = 2.50, Maximum = 999999.99 } },
        { "EUR", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "GBP", new StripePaymentLimits { Minimum = 0.30, Maximum = 999999.99 } },
        { "HKD", new StripePaymentLimits { Minimum = 4.00, Maximum = 999999.99 } },
        { "HUF", new StripePaymentLimits { Minimum = 175.00, Maximum = 999999.99 } },
        { "INR", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "JPY", new StripePaymentLimits { Minimum = 50.00, Maximum = 99999999.00 } },
        { "MXN", new StripePaymentLimits { Minimum = 10.00, Maximum = 999999.99 } },
        { "MYR", new StripePaymentLimits { Minimum = 2.00, Maximum = 999999.99 } },
        { "NOK", new StripePaymentLimits { Minimum = 3.00, Maximum = 999999.99 } },
        { "NZD", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "PLN", new StripePaymentLimits { Minimum = 2.00, Maximum = 999999.99 } },
        { "RUB", new StripePaymentLimits { Minimum = 20.00, Maximum = 999999.99 } },
        { "SEK", new StripePaymentLimits { Minimum = 3.00, Maximum = 999999.99 } },
        { "SGD", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "THB", new StripePaymentLimits { Minimum = 10.00, Maximum = 999999.99 } },
        { "USD", new StripePaymentLimits { Minimum = 0.50, Maximum = 999999.99 } },
        { "ZAR", new StripePaymentLimits { Minimum = 8.00, Maximum = 999999.99 } },
    };

    public static StripePaymentLimits GetStripePaymentLimit(string currency)
    {
        ArgumentException.ThrowIfNullOrEmpty(currency);

        if (StripePaymentLimits.TryGetValue(currency, out var stripePaymentLimits))
        {
            return stripePaymentLimits;
        }

        return null;
    }

    public static bool TryGetStripePaymentLimit(string currency, out StripePaymentLimits limits)
    {
        if (currency != null)
        {
            return StripePaymentLimits.TryGetValue(currency, out limits);
        }

        limits = null;

        return false;
    }
}
