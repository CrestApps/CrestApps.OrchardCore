using CrestApps.OrchardCore.Downloads.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Downloads.Indexes;

public sealed class DownloadEntitlementIndex : MapIndex
{
    public string ItemId { get; set; }

    public string OrderId { get; set; }

    public string FileId { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class DownloadEntitlementIndexProvider : IndexProvider<DownloadEntitlement>
{
    public DownloadEntitlementIndexProvider()
    {
        CollectionName = DownloadsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<DownloadEntitlement> context)
    {
        context.For<DownloadEntitlementIndex>()
            .Map(entitlement => new DownloadEntitlementIndex
            {
                ItemId = entitlement.ItemId,
                OrderId = entitlement.OrderId,
                FileId = entitlement.FileId,
                CreatedUtc = entitlement.CreatedUtc,
            });
    }
}
