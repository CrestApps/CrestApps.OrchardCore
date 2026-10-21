using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Markets.Services;

/// <summary>
/// A product left out of the shopper's market can't be bought there.
/// </summary>
public sealed class MarketSaleRestriction : IProductSaleRestriction
{
    private readonly ShopperMarketService _markets;

    internal readonly IStringLocalizer S;

    public MarketSaleRestriction(
        ShopperMarketService markets,
        IStringLocalizer<MarketSaleRestriction> stringLocalizer)
    {
        _markets = markets;
        S = stringLocalizer;
    }

    public async ValueTask<string> GetRestrictionAsync(ContentItem product, CancellationToken cancellationToken = default)
    {
        if (product is null ||
            !product.TryGet<ProductMarketsPart>(out var part) ||
            part.NotSoldIn is not { Count: > 0 } ||
            await _markets.GetCurrentAsync(cancellationToken) is not { } market ||
            !part.NotSoldIn.Contains(market.ItemId, StringComparer.Ordinal))
        {
            return null;
        }

        return S["Not sold in {0}.", market.Name];
    }
}
