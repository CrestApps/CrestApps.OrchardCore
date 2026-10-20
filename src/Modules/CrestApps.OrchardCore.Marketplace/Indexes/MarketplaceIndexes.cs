using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Marketplace.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Marketplace.Indexes;

public sealed class VendorIndex : CatalogItemIndex
{
    public string Name { get; set; }

    public VendorStatus Status { get; set; }
}

/// <summary>
/// One row per user who runs a vendor, so a user's vendors are found.
/// </summary>
public sealed class VendorUserIndex : MapIndex
{
    public string VendorId { get; set; }

    public string UserId { get; set; }
}

public sealed class VendorEarningIndex : CatalogItemIndex
{
    public string VendorId { get; set; }

    public string OrderId { get; set; }

    public VendorEarningStatus Status { get; set; }

    public DateTime HoldUntilUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class VendorPayoutIndex : CatalogItemIndex
{
    public string VendorId { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class VendorIndexProvider : IndexProvider<Vendor>
{
    public VendorIndexProvider()
    {
        CollectionName = MarketplaceConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Vendor> context)
    {
        context.For<VendorIndex>()
            .Map(vendor => new VendorIndex
            {
                ItemId = vendor.ItemId,
                Name = vendor.Name?.Length > 255 ? vendor.Name[..255] : vendor.Name,
                Status = vendor.Status,
            });

        context.For<VendorUserIndex>()
            .Map(vendor => (vendor.UserIds ?? []).Select(userId => new VendorUserIndex { VendorId = vendor.ItemId, UserId = userId }));
    }
}

public sealed class VendorEarningIndexProvider : IndexProvider<VendorEarning>
{
    public VendorEarningIndexProvider()
    {
        CollectionName = MarketplaceConstants.CollectionName;
    }

    public override void Describe(DescribeContext<VendorEarning> context)
        => context.For<VendorEarningIndex>()
            .Map(earning => new VendorEarningIndex
            {
                ItemId = earning.ItemId,
                VendorId = earning.VendorId,
                OrderId = earning.OrderId,
                Status = earning.Status,
                HoldUntilUtc = earning.HoldUntilUtc,
                CreatedUtc = earning.CreatedUtc,
            });
}

public sealed class VendorPayoutIndexProvider : IndexProvider<VendorPayout>
{
    public VendorPayoutIndexProvider()
    {
        CollectionName = MarketplaceConstants.CollectionName;
    }

    public override void Describe(DescribeContext<VendorPayout> context)
        => context.For<VendorPayoutIndex>()
            .Map(payout => new VendorPayoutIndex
            {
                ItemId = payout.ItemId,
                VendorId = payout.VendorId,
                CreatedUtc = payout.CreatedUtc,
            });
}
