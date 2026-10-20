using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Markets.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Markets.Indexes;

public sealed class MarketIndex : CatalogItemIndex
{
    public string Name { get; set; }

    public string Currency { get; set; }

    public bool IsEnabled { get; set; }
}

public sealed class MarketIndexProvider : IndexProvider<Market>
{
    public MarketIndexProvider()
    {
        CollectionName = MarketsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Market> context)
        => context.For<MarketIndex>()
            .Map(market => new MarketIndex
            {
                ItemId = market.ItemId,
                Name = market.Name,
                Currency = market.Currency,
                IsEnabled = market.IsEnabled,
            });
}
