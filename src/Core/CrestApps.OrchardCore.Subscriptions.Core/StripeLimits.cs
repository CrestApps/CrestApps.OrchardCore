using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core;

/// <summary>
/// Provides Stripe payment amount limits for supported currencies.
/// </summary>
public class StripeLimits
{
    /// <summary>
    /// Contains Stripe payment amount limits keyed by ISO currency code.
    /// </summary>
    public static readonly Dictionary<string, StripePaymentLimits> StripePaymentLimits = new()
    {
        { "AED", new StripePaymentLimits { Minimum = 2.00m, Maximum = 999999.99m } },
        { "AUD", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "BRL", new StripePaymentLimits { Minimum = 0.50m, Maximum = 50000.00m } },
        { "CAD", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "CHF", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "CNY", new StripePaymentLimits { Minimum = 0.01m, Maximum = 999999.99m } },
        { "CZK", new StripePaymentLimits { Minimum = 15.00m, Maximum = 999999.99m } },
        { "DKK", new StripePaymentLimits { Minimum = 2.50m, Maximum = 999999.99m } },
        { "EUR", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "GBP", new StripePaymentLimits { Minimum = 0.30m, Maximum = 999999.99m } },
        { "HKD", new StripePaymentLimits { Minimum = 4.00m, Maximum = 999999.99m } },
        { "HUF", new StripePaymentLimits { Minimum = 175.00m, Maximum = 999999.99m } },
        { "INR", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "JPY", new StripePaymentLimits { Minimum = 50.00m, Maximum = 99999999.00m } },
        { "MXN", new StripePaymentLimits { Minimum = 10.00m, Maximum = 999999.99m } },
        { "MYR", new StripePaymentLimits { Minimum = 2.00m, Maximum = 999999.99m } },
        { "NOK", new StripePaymentLimits { Minimum = 3.00m, Maximum = 999999.99m } },
        { "NZD", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "PLN", new StripePaymentLimits { Minimum = 2.00m, Maximum = 999999.99m } },
        { "RUB", new StripePaymentLimits { Minimum = 20.00m, Maximum = 999999.99m } },
        { "SEK", new StripePaymentLimits { Minimum = 3.00m, Maximum = 999999.99m } },
        { "SGD", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "THB", new StripePaymentLimits { Minimum = 10.00m, Maximum = 999999.99m } },
        { "USD", new StripePaymentLimits { Minimum = 0.50m, Maximum = 999999.99m } },
        { "ZAR", new StripePaymentLimits { Minimum = 8.00m, Maximum = 999999.99m } },
    };

    /// <summary>
    /// Gets the Stripe payment amount limits for the specified currency.
    /// </summary>
    /// <param name="currency">The ISO currency code to look up.</param>
    /// <returns>The Stripe payment limits for the currency, or <see langword="null"/> when the currency is not supported.</returns>
    public static StripePaymentLimits GetStripePaymentLimit(string currency)
    {
        ArgumentException.ThrowIfNullOrEmpty(currency);

        if (StripePaymentLimits.TryGetValue(currency, out var stripePaymentLimits))
        {
            return stripePaymentLimits;
        }

        return null;
    }

    /// <summary>
    /// Attempts to get the Stripe payment amount limits for the specified currency.
    /// </summary>
    /// <param name="currency">The ISO currency code to look up.</param>
    /// <param name="limits">When this method returns, contains the matching payment limits, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when limits are found for the currency; otherwise, <see langword="false"/>.</returns>
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
