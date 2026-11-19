using System.Globalization;
using CrestApps.OrchardCore.Currencies;

namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// The default <see cref="IMoneyFormatter"/>: the symbol of the currency in the number format of the reader's culture,
/// at the precision of the currency.
/// </summary>
public sealed class DefaultMoneyFormatter : IMoneyFormatter
{
    /// <inheritdoc/>
    public string Format(decimal amount, string currency, CultureInfo culture = null)
        => CurrencyFormat.WithSymbol(amount, currency, culture);
}
