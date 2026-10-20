using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Fundraising.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Fundraising.Indexes;

public sealed class DonationIndex : CatalogItemIndex
{
    public string CampaignId { get; set; }

    public string OrderId { get; set; }

    public string DonorUserId { get; set; }

    public decimal CampaignAmount { get; set; }

    public decimal Refunded { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class DonationIndexProvider : IndexProvider<Donation>
{
    public DonationIndexProvider()
    {
        CollectionName = FundraisingConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Donation> context)
        => context.For<DonationIndex>()
            .Map(donation => new DonationIndex
            {
                ItemId = donation.ItemId,
                CampaignId = donation.CampaignId,
                OrderId = donation.OrderId,
                DonorUserId = donation.DonorUserId,
                CampaignAmount = donation.CampaignAmount,
                Refunded = donation.Refunded,
                CreatedUtc = donation.CreatedUtc,
            });
}
