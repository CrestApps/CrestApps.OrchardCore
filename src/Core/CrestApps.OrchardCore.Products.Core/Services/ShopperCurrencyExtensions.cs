namespace CrestApps.OrchardCore.Products.Core.Services;

public static class ShopperCurrencyExtensions
{
    /// <summary>
    /// Returns an amount as the shopper sees it: converted into their currency when they buy in another one, and as it
    /// is otherwise, or when it can't be converted.
    /// </summary>
    public static async ValueTask<(decimal Amount, string Currency)> ToShopperAsync(this IShopperCurrency shopperCurrency, decimal amount, string currency, bool isPrice = true, CancellationToken cancellationToken = default)
    {
        var target = shopperCurrency is null ? null : await shopperCurrency.GetCurrencyAsync(cancellationToken);

        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(currency) || string.Equals(target, currency, StringComparison.OrdinalIgnoreCase))
        {
            return (amount, currency);
        }

        return await shopperCurrency.ConvertAsync(amount, currency, target, isPrice, cancellationToken) is { } converted
            ? (converted, target.ToUpperInvariant())
            : (amount, currency);
    }

    /// <summary>
    /// Returns an amount formatted as the shopper sees it: converted into their currency when they buy in another one,
    /// and as it is otherwise, or when it can't be converted.
    /// </summary>
    /// <param name="shopperCurrency">The shopper's currency.</param>
    /// <param name="amount">The amount, in <paramref name="currency"/>.</param>
    /// <param name="currency">The amount's currency.</param>
    /// <param name="isPrice">Whether the amount is a price, rounded by the store's price rounding rule when converted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async ValueTask<string> FormatForShopperAsync(this IShopperCurrency shopperCurrency, decimal amount, string currency, bool isPrice = true, CancellationToken cancellationToken = default)
    {
        var (shown, shownCurrency) = await shopperCurrency.ToShopperAsync(amount, currency, isPrice, cancellationToken);

        return ProductPriceFormatter.Format(shown, shownCurrency);
    }

    /// <summary>
    /// Returns an amount converted from one currency into another, or as it is when either currency is missing, they are
    /// the same, or it can't be converted.
    /// </summary>
    /// <param name="shopperCurrency">The shopper's currency.</param>
    /// <param name="amount">The amount, in <paramref name="fromCurrency"/>.</param>
    /// <param name="fromCurrency">The amount's currency.</param>
    /// <param name="toCurrency">The currency to convert the amount into.</param>
    /// <param name="isPrice">Whether the amount is a price, rounded by the store's price rounding rule when converted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async ValueTask<decimal> ConvertOrKeepAsync(this IShopperCurrency shopperCurrency, decimal amount, string fromCurrency, string toCurrency, bool isPrice = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shopperCurrency);

        if (string.IsNullOrEmpty(fromCurrency) || string.IsNullOrEmpty(toCurrency) || string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        return await shopperCurrency.ConvertAsync(amount, fromCurrency, toCurrency, isPrice, cancellationToken) ?? amount;
    }
}
