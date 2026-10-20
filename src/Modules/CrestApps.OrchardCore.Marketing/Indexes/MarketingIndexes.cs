using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Marketing.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Marketing.Indexes;

/// <summary>
/// A customer's purchases, for segments to query: how many orders, what they spent, when they first and last bought,
/// and where.
/// </summary>
public sealed class CustomerPurchasesIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document's id, which campaigns send in the order of.
    /// </summary>
    public long DocumentId { get; set; }

    public int OrderCount { get; set; }

    public decimal TotalSpent { get; set; }

    public DateTime? FirstOrderUtc { get; set; }

    public DateTime? LastOrderUtc { get; set; }

    public string Country { get; set; }
}

public sealed class SegmentIndex : CatalogItemIndex
{
    public string Name { get; set; }
}

public sealed class CampaignIndex : CatalogItemIndex
{
    public string Name { get; set; }

    public CampaignStatus Status { get; set; }

    public DateTime? ScheduledUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class MarketingIndexProvider : IndexProvider<CustomerPurchases>
{
    public MarketingIndexProvider()
    {
        CollectionName = MarketingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<CustomerPurchases> context)
        => context.For<CustomerPurchasesIndex>()
            .Map(purchases => new CustomerPurchasesIndex
            {
                ItemId = purchases.ItemId,
                OrderCount = purchases.OrderCount,
                TotalSpent = purchases.TotalSpent,
                FirstOrderUtc = purchases.FirstOrderUtc,
                LastOrderUtc = purchases.LastOrderUtc,
                Country = purchases.Country,
            });
}

public sealed class SegmentIndexProvider : IndexProvider<Segment>
{
    public SegmentIndexProvider()
    {
        CollectionName = MarketingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Segment> context)
        => context.For<SegmentIndex>()
            .Map(segment => new SegmentIndex { ItemId = segment.ItemId, Name = segment.Name });
}

public sealed class CampaignIndexProvider : IndexProvider<Campaign>
{
    public CampaignIndexProvider()
    {
        CollectionName = MarketingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Campaign> context)
        => context.For<CampaignIndex>()
            .Map(campaign => new CampaignIndex
            {
                ItemId = campaign.ItemId,
                Name = campaign.Name,
                Status = campaign.Status,
                ScheduledUtc = campaign.ScheduledUtc,
                CreatedUtc = campaign.CreatedUtc,
            });
}
