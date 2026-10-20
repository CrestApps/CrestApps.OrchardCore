using CrestApps.OrchardCore.Carts.Wishlists;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// A wishlist keeps each product once, newest first, and its newest 100.
/// </summary>
public sealed class WishlistTests
{
    [Fact]
    public async Task ASavedProduct_GoesToTheTop_Once_AndTheOldestDropOffPastTheLimit()
    {
        Wishlist stored = null;
        var store = new Mock<IWishlistStore>();
        store.Setup(value => value.FindByUserAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(() => stored);
        store.Setup(value => value.CreateAsync(It.IsAny<Wishlist>(), It.IsAny<CancellationToken>())).Callback<Wishlist, CancellationToken>((wishlist, _) => stored = wishlist);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc));

        var service = new WishlistService(store.Object, clock.Object);

        for (var i = 0; i < Wishlist.MaxItems + 5; i++)
        {
            await service.AddAsync("user-1", $"product-{i}", TestContext.Current.CancellationToken);
        }

        await service.AddAsync("user-1", "product-50", TestContext.Current.CancellationToken);

        Assert.Equal(Wishlist.MaxItems, stored.Items.Count);
        Assert.Equal("product-50", stored.Items[0].ContentItemId);
        Assert.Single(stored.Items, item => item.ContentItemId == "product-50");
        Assert.DoesNotContain(stored.Items, item => item.ContentItemId == "product-0");

        await service.RemoveAsync("user-1", "product-50", TestContext.Current.CancellationToken);

        Assert.False(await service.ContainsAsync("user-1", "product-50", TestContext.Current.CancellationToken));
    }
}
