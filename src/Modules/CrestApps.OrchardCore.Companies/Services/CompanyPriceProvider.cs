using CrestApps.OrchardCore.Companies.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Services;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// Gives a company's buyers the prices of the company's price list: a SKU's own price on the list, converted to the
/// buyer's currency when they shop in another, or else the list's percentage off.
/// </summary>
public sealed class CompanyPriceProvider : IBuyerPriceProvider
{
    private readonly CompanyService _companies;
    private readonly IPriceListStore _priceLists;
    private readonly IShopperCurrency _shopperCurrency;
    private readonly Dictionary<string, PriceList> _lists = new(StringComparer.Ordinal);

    public CompanyPriceProvider(CompanyService companies, IPriceListStore priceLists, IShopperCurrency shopperCurrency = null)
    {
        _companies = companies;
        _priceLists = priceLists;
        _shopperCurrency = shopperCurrency;
    }

    public async ValueTask<decimal?> GetUnitPriceAsync(BuyerPriceContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if ((await _companies.GetCurrentAsync(cancellationToken)).Company is not { PriceListId.Length: > 0 } company ||
            await GetListAsync(company.PriceListId, cancellationToken) is not { } list)
        {
            return null;
        }

        return await PriceAsync(list, context, _shopperCurrency, cancellationToken);
    }

    /// <summary>
    /// Returns a price list's price for a product, in the product's currency, or <see langword="null"/> when the list
    /// doesn't change it.
    /// </summary>
    public static async ValueTask<decimal?> PriceAsync(PriceList list, BuyerPriceContext context, IShopperCurrency shopperCurrency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(context);

        if (list.Find(context.Sku) is { } entry)
        {
            if (string.IsNullOrEmpty(list.Currency) || string.Equals(list.Currency, context.Currency, StringComparison.OrdinalIgnoreCase))
            {
                return entry.UnitPrice;
            }

            // A list in the store's currency, for a buyer shopping in their market's.
            if (shopperCurrency is not null &&
                await shopperCurrency.ConvertAsync(entry.UnitPrice, list.Currency, context.Currency, isPrice: true, cancellationToken) is { } converted)
            {
                return converted;
            }
        }

        return list.PercentOff is > 0m and <= 100m
            ? Money.Round(context.UnitPrice * (100m - list.PercentOff.Value) / 100m, context.Currency)
            : null;
    }

    private async Task<PriceList> GetListAsync(string priceListId, CancellationToken cancellationToken)
    {
        if (!_lists.TryGetValue(priceListId, out var list))
        {
            list = await _priceLists.FindByIdAsync(priceListId, cancellationToken);
            _lists[priceListId] = list;
        }

        return list;
    }
}
