using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Subscriptions.Extensions;

/// <summary>
/// Formats prices for the subscription screens.
/// </summary>
public static class PriceFormatting
{
    /// <summary>
    /// Formats a price in the currency it is charged in, for example <c>EUR 20.00</c>. A caller passes the store's
    /// currency for a price that names none; without any, the amount is shown alone, never in the server's currency.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The ISO-4217 code of the currency the amount is in.</param>
    /// <returns>The formatted price.</returns>
    public static string Format(decimal amount, string currency)
        => string.IsNullOrEmpty(currency)
            ? CurrencyScale.Format(amount, currency)
            : $"{currency} {CurrencyScale.Format(amount, currency)}";
}
