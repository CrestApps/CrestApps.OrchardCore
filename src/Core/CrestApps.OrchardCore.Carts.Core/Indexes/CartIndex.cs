using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Carts.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Carts.Core.Indexes;

/// <summary>
/// Finds a cart by its owner, and the carts that have expired.
/// </summary>
public sealed class CartIndex : CatalogItemIndex
{
    public string UserId { get; set; }

    public string GuestTokenHash { get; set; }

    public DateTime ExpiresUtc { get; set; }
}

public sealed class CartIndexProvider : IndexProvider<Cart>
{
    public CartIndexProvider()
    {
        CollectionName = CartConstants.CollectionName;
    }

    public override void Describe(DescribeContext<Cart> context)
    {
        context.For<CartIndex>()
            .Map(cart => new CartIndex
            {
                ItemId = cart.ItemId,
                UserId = cart.UserId,
                GuestTokenHash = cart.GuestTokenHash,
                ExpiresUtc = cart.ExpiresUtc,
            });
    }
}
