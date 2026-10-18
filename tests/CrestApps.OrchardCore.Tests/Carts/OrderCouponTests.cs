using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Handlers;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Carts;

/// <summary>
/// What orders give coupons: the categories each product was in, and their uses back when they are canceled.
/// </summary>
public sealed class OrderCouponTests
{
    [Fact]
    public void AProductsCategories_AreTheTermsOfEveryTaxonomyField()
    {
        var product = new ContentItem { ContentItemId = "product-1", ContentType = "Product" };

        product.Content.Product = JObject.FromObject(new
        {
            Category = new { TaxonomyContentItemId = "taxonomy-1", TermContentItemIds = new[] { "term-hats", "term-summer" } },
            Brand = new { TaxonomyContentItemId = "taxonomy-2", TermContentItemIds = new[] { "term-contoso", "term-hats" } },
        });

        Assert.Equal(["term-hats", "term-summer", "term-contoso"], ProductCategories.GetIds(product));
        Assert.Empty(ProductCategories.GetIds(null));
    }

    [Fact]
    public async Task ACanceledOrder_GivesBackItsCheckoutsCouponUses()
    {
        var redemptions = new Mock<ICouponRedemptionService>();
        var handler = new OrderCouponEventHandler(redemptions.Object);

        await handler.CanceledAsync(new OrderCanceledContext(new Order { ItemId = "order-1", CheckoutSessionId = "session-1" }, OrderCancelReason.CheckoutCanceled), TestContext.Current.CancellationToken);
        await handler.CanceledAsync(new OrderCanceledContext(new Order { ItemId = "order-2" }, OrderCancelReason.Replaced), TestContext.Current.CancellationToken);

        redemptions.Verify(service => service.ReturnAsync("session-1", It.IsAny<CancellationToken>()), Times.Once);
        redemptions.VerifyNoOtherCalls();
    }
}
