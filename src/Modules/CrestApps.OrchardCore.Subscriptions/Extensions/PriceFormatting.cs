using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Extensions;

/// <summary>
/// Formats prices for the subscription screens.
/// </summary>
public static class PriceFormatting
{
    /// <summary>
    /// Formats a price in the currency it is charged in, for example <c>EUR 20.00</c>. The server's culture only
    /// decides the symbol when the price names no currency, as older plans may not.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The ISO-4217 code of the currency the amount is in.</param>
    /// <returns>The formatted price.</returns>
    public static string Format(decimal amount, string currency)
        => string.IsNullOrEmpty(currency)
            ? amount.ToString("C")
            : $"{currency} {CurrencyScale.Format(amount, currency)}";
}
