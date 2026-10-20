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
}
