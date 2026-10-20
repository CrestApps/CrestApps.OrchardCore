using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
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
public sealed class WishlistMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<WishlistIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26)),
            collection: CartConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<WishlistIndex>(SchemaBuilder, "IDX_WishlistIndex_UserId", CartConstants.CollectionName, "UserId");

        return 1;
    }
}

public interface IWishlistStore : ICatalog<Wishlist>
{
    Task<Wishlist> FindByUserAsync(string userId, CancellationToken cancellationToken = default);
}

public sealed class WishlistStore : DocumentCatalog<Wishlist, WishlistIndex>, IWishlistStore
{
    public WishlistStore(ISession session)
        : base(session)
    {
        CollectionName = CartConstants.CollectionName;
    }

    public Task<Wishlist> FindByUserAsync(string userId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(userId)
            ? Task.FromResult<Wishlist>(null)
            : Session.Query<Wishlist, WishlistIndex>(index => index.UserId == userId, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
}

/// <summary>
/// Saves products to a signed-in shopper's wishlist, newest first, and takes them off.
/// </summary>
public sealed class WishlistService
{
    private readonly IWishlistStore _store;
    private readonly IClock _clock;

    public WishlistService(IWishlistStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public Task<Wishlist> GetAsync(string userId, CancellationToken cancellationToken = default)
        => _store.FindByUserAsync(userId, cancellationToken);

    public async Task<bool> ContainsAsync(string userId, string contentItemId, CancellationToken cancellationToken = default)
        => (await GetAsync(userId, cancellationToken))?.Items.Any(item => item.ContentItemId == contentItemId) == true;

    /// <summary>
    /// Saves a product to the wishlist, at the top. A wishlist keeps its newest 100.
    /// </summary>
    public async Task AddAsync(string userId, string contentItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(contentItemId);

        var wishlist = await GetAsync(userId, cancellationToken);
        var isNew = wishlist is null;
        var now = _clock.UtcNow;

        wishlist ??= new Wishlist { ItemId = IdGenerator.GenerateId(), UserId = userId };
        wishlist.Items.RemoveAll(item => item.ContentItemId == contentItemId);
        wishlist.Items.Insert(0, new WishlistItem { ContentItemId = contentItemId, AddedUtc = now });

        if (wishlist.Items.Count > Wishlist.MaxItems)
        {
            wishlist.Items.RemoveRange(Wishlist.MaxItems, wishlist.Items.Count - Wishlist.MaxItems);
        }

        wishlist.ModifiedUtc = now;

        if (isNew)
        {
            await _store.CreateAsync(wishlist, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(wishlist, cancellationToken);
        }
    }

    public async Task RemoveAsync(string userId, string contentItemId, CancellationToken cancellationToken = default)
    {
        if (await GetAsync(userId, cancellationToken) is not { } wishlist || wishlist.Items.RemoveAll(item => item.ContentItemId == contentItemId) == 0)
        {
            return;
        }

        wishlist.ModifiedUtc = _clock.UtcNow;
        await _store.UpdateAsync(wishlist, cancellationToken);
    }
}
