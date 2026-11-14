using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Customers.Indexes;

/// <summary>
/// The queryable projection of a <see cref="StoreCreditAccount"/>.
/// </summary>
public sealed class StoreCreditAccountIndex : CatalogItemIndex
{
    public string CustomerId { get; set; }

    public string PartyKind { get; set; }

    public string PartyId { get; set; }

    public string Currency { get; set; }

    public decimal Balance { get; set; }
}

/// <summary>
/// Maps <see cref="StoreCreditAccount"/> documents to <see cref="StoreCreditAccountIndex"/> rows.
/// </summary>
public sealed class StoreCreditAccountIndexProvider : IndexProvider<StoreCreditAccount>
{
    public StoreCreditAccountIndexProvider()
    {
        CollectionName = CustomersConstants.CollectionName;
    }

    public override void Describe(DescribeContext<StoreCreditAccount> context)
    {
        context.For<StoreCreditAccountIndex>()
            .Map(account => new StoreCreditAccountIndex
            {
                ItemId = account.ItemId,
                // The column predates party accounts and takes no null, so a party's account indexes an empty customer.
                CustomerId = account.CustomerId ?? string.Empty,
                PartyKind = string.IsNullOrEmpty(account.PartyKind) ? null : account.PartyKind,
                PartyId = string.IsNullOrEmpty(account.PartyId) ? null : account.PartyId,
                Currency = account.Currency,
                Balance = account.Balance,
            });
    }
}
