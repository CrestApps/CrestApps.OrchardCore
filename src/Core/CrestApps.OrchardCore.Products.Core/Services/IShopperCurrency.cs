namespace CrestApps.OrchardCore.Products.Core.Services;

/// <summary>
/// The currency the shopper buys in, and how a price in a product's own currency becomes one in it. The default sells
/// every product in its own currency and converts nothing; a markets feature replaces it with the shopper's market.
/// </summary>
public interface IShopperCurrency
{
    /// <summary>
    /// Returns the currency the current shopper buys in, or <see langword="null"/> when each product sells in its own.
    /// </summary>
    ValueTask<string> GetCurrencyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <paramref name="amount"/>, in <paramref name="fromCurrency"/> (or the store's own currency when it's
    /// <see langword="null"/>), as the shopper pays it in <paramref name="toCurrency"/>. A price is rounded by the store's price rounding rule, such as ending in .99;
    /// another amount, such as what an add-on adds, only to the currency's smallest unit. Returns
    /// <see langword="null"/> when it can't be converted.
    /// </summary>
    ValueTask<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, bool isPrice = true, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sells every product in its own currency.
/// </summary>
public sealed class ProductOwnShopperCurrency : IShopperCurrency
{
    public ValueTask<string> GetCurrencyAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<string>(null);

    public ValueTask<decimal?> ConvertAsync(decimal amount, string fromCurrency, string toCurrency, bool isPrice = true, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase) ? amount : (decimal?)null);
}
