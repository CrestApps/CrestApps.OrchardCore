using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using YesSql;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Carts.Wishlists;

/// <summary>
/// The products a signed-in shopper saved for later.
/// </summary>
public sealed class Wishlist : CatalogItem
{
    public const int MaxItems = 100;

    public string UserId { get; set; }

    public List<WishlistItem> Items { get; set; } = [];

    public DateTime ModifiedUtc { get; set; }
}

public sealed class WishlistItem
{
    public string ContentItemId { get; set; }

    public DateTime AddedUtc { get; set; }
}

public sealed class WishlistIndex : CatalogItemIndex
{
    public string UserId { get; set; }
}

public sealed class WishlistIndexProvider : IndexProvider<Wishlist>
{
    public WishlistIndexProvider()
    {
        CollectionName = CartConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Wishlist> context)
        => context.For<WishlistIndex>()
            .Map(wishlist => new WishlistIndex { ItemId = wishlist.ItemId, UserId = wishlist.UserId });
}

/// <summary>
/// Creates the index of wishlists.
/// </summary>
internal sealed class WishlistMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WishlistIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26)),
            collection: CartConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<WishlistIndex>(table => table
            .CreateIndex("IDX_WishlistIndex_UserId", "UserId"),
            collection: CartConstants.CollectionName
        );

        return 1;
    }
}
