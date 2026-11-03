using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using OrchardCore.ContentManagement;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Accounts.B2B.Indexes;

/// <summary>
/// One row for each version of an account that has the B2B part, so the accounts that buy as a business, and the ones
/// on a price list, are found without reading every account.
/// </summary>
public sealed class B2BAccountIndex : MapIndex
{
    public string ContentItemId { get; set; }

    public bool Enabled { get; set; }

    public string PriceListId { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}

/// <summary>
/// One row for each of an account's buyers, so a signed-in user's account is found by their user id.
/// </summary>
public sealed class B2BAccountBuyerIndex : MapIndex
{
    public string AccountContentItemId { get; set; }

    public string UserId { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}

public sealed class PriceListIndex : CatalogItemIndex
{
    public string Name { get; set; }
}

public sealed class PriceListIndexProvider : IndexProvider<PriceList>
{
    public PriceListIndexProvider()
    {
        CollectionName = B2BAccountsConstants.PriceListsCollectionName;
    }

    public override void Describe(DescribeContext<PriceList> context)
        => context.For<PriceListIndex>()
            .Map(list => new PriceListIndex
            {
                ItemId = list.ItemId,
                Name = list.Name,
            });
}

/// <summary>
/// Indexes the accounts that have the B2B part, and their buyers.
/// </summary>
public sealed class B2BAccountIndexProvider : IndexProvider<ContentItem>
{
    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<B2BAccountIndex>()
            .When(contentItem => contentItem.Has<B2BAccountPart>())
            .Map(contentItem =>
            {
                var part = Part(contentItem);

                return new B2BAccountIndex
                {
                    ContentItemId = contentItem.ContentItemId,
                    Enabled = part.Enabled,
                    PriceListId = part.PriceListId,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                };
            });

        context.For<B2BAccountBuyerIndex>()
            .When(contentItem => contentItem.Has<B2BAccountPart>())
            .Map(contentItem => (Part(contentItem).Buyers ?? [])
                .Where(buyer => !string.IsNullOrEmpty(buyer.UserId))
                .Select(buyer => new B2BAccountBuyerIndex
                {
                    AccountContentItemId = contentItem.ContentItemId,
                    UserId = buyer.UserId,
                    Published = contentItem.Published,
                    Latest = contentItem.Latest,
                }));
    }

    private static B2BAccountPart Part(ContentItem contentItem)
        => contentItem.TryGet<B2BAccountPart>(out var part) ? part : new B2BAccountPart();
}
