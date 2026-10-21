using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Customers.Indexes;

/// <summary>
/// The queryable projection of a <see cref="LoyaltyAccount"/>.
/// </summary>
public sealed class LoyaltyAccountIndex : CatalogItemIndex
{
    public string CustomerId { get; set; }

    public int Points { get; set; }
}

/// <summary>
/// Maps <see cref="LoyaltyAccount"/> documents to <see cref="LoyaltyAccountIndex"/> rows.
/// </summary>
public sealed class LoyaltyAccountIndexProvider : IndexProvider<LoyaltyAccount>
{
    public LoyaltyAccountIndexProvider()
    {
        CollectionName = CustomersConstants.CollectionName;
    }

    public override void Describe(DescribeContext<LoyaltyAccount> context)
    {
        context.For<LoyaltyAccountIndex>()
            .Map(account => new LoyaltyAccountIndex
            {
                ItemId = account.ItemId,
                CustomerId = account.CustomerId,
                Points = account.Points,
            });
    }
}
