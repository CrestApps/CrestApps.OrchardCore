using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Customers.Indexes;

/// <summary>
/// The queryable projection of a <see cref="ReferralProfile"/>.
/// </summary>
public sealed class ReferralProfileIndex : CatalogItemIndex
{
    public string CustomerId { get; set; }

    public string Code { get; set; }
}

/// <summary>
/// Maps <see cref="ReferralProfile"/> documents to <see cref="ReferralProfileIndex"/> rows.
/// </summary>
public sealed class ReferralProfileIndexProvider : IndexProvider<ReferralProfile>
{
    public ReferralProfileIndexProvider()
    {
        CollectionName = CustomersConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ReferralProfile> context)
    {
        context.For<ReferralProfileIndex>()
            .Map(profile => new ReferralProfileIndex
            {
                ItemId = profile.ItemId,
                CustomerId = profile.CustomerId,
                Code = profile.Code,
            });
    }
}
