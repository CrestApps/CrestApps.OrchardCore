using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Handlers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// An order's goods are priced for shipping by what the version bought weighs, and the order is taxed where it goes.
/// </summary>
public sealed class ShippingCheckoutTests
{
    private static readonly Address _seattle = new() { Country = "US", Region = "WA", AddressLine1 = "9 Pine Rd" };

    [Fact]
    public async Task ARateRequest_HoldsTheGoods_WithTheWeightOfTheVersionBoughtAndOfEachVariant()
    {
        var cap = Product("cap-v1", new ShippingPart { Weight = 0.3m, ShippingClass = "Bulky", VariantWeights = { ["large"] = 0.4m } });
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetVersionAsync("cap-v1")).ReturnsAsync(cap);

        var order = new Order
        {
            Currency = "USD",
            Lines =
            [
                Line("cap", "cap-v1", "large", 2, 20m, ProductType.Good),
                Line("cap", "cap-v1", "small", 1, 18m, ProductType.Good),
                Line("ebook", "ebook-v1", null, 1, 9m, ProductType.Digital),
                Line("setup", "setup-v1", null, 1, 50m, ProductType.Service),
            ],
        };

        var request = await new ShippingOrderRateService(Mock.Of<IShippingRateService>(), contentManager.Object)
            .BuildRequestAsync(order, _seattle, TestContext.Current.CancellationToken);

        Assert.Same(_seattle, request.Destination);
        Assert.Equal("USD", request.Currency);
        Assert.Equal([("large", 2, 0.4m), ("small", 1, 0.3m)], request.Lines.Select(line => (line.VariantId, line.Quantity, line.Weight)));
        Assert.All(request.Lines, line => Assert.Equal("Bulky", line.ShippingClass));
        Assert.Equal(58m, request.Subtotal);
        Assert.Equal(1.1m, request.TotalWeight);

        // The product's page is read once for both lines.
        contentManager.Verify(manager => manager.GetVersionAsync("cap-v1"), Times.Once);
    }

    [Fact]
    public void OnlyOrdersWithGoods_Ship()
    {
        Assert.True(ShippingOrderRateService.Ships(new Order { Lines = [Line("cap", "v", null, 1, 1, ProductType.Good)] }));
        Assert.False(ShippingOrderRateService.Ships(new Order { Lines = [Line("ebook", "v", null, 1, 1, ProductType.Digital), Line("setup", "v", null, 1, 1, ProductType.Service)] }));
        Assert.False(ShippingOrderRateService.Ships(null));
    }

    [Theory]
    [InlineData(ProductType.Good, true)]
    [InlineData(ProductType.Digital, false)]
    [InlineData(ProductType.Service, false)]
    public async Task TheShippingStep_IsAddedOnlyToOrdersWithGoods(ProductType type, bool added)
    {
        var order = new Order { ItemId = "order-1", Lines = [Line("item", "v", null, 1, 10m, type)] };
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var handler = new ShippingCheckoutHandler(
            orders.Object,
            new ServiceCollection().BuildServiceProvider(),
            Options.Create(new DocumentJsonSerializerOptions()),
            new PassThroughStringLocalizer<ShippingCheckoutHandler>());

        var session = new CheckoutSession { SessionId = "s", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1" };
        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));

        Assert.Equal(added, session.Steps.Any(step => step.Key == ShippingConstants.CheckoutStepKey && step.CollectData && step.Order == 2));

        // A checkout of something other than an order is never shipped.
        var other = new CheckoutSession { SessionId = "t", ReferenceType = "Subscription", ReferenceId = "order-1" };
        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(other));

        Assert.Empty(other.Steps);
    }

    [Fact]
    public void AnOrder_IsTaxedWhereItShips_ElseWhereItIsBilled()
    {
        var billing = new Address { Country = "CA", Region = "ON" };
        var session = new CheckoutSession();

        Assert.Null(DefaultCheckoutTaxProfileProvider.GetDestination(session));

        session.Put(new CheckoutContactInfo { BillingAddress = billing });
        Assert.Equal(("CA", "ON"), (DefaultCheckoutTaxProfileProvider.GetDestination(session).Country, DefaultCheckoutTaxProfileProvider.GetDestination(session).Region));

        session.Put(new CheckoutShippingInfo { Address = _seattle, Name = "Standard" });
        Assert.Equal(("US", "WA"), (DefaultCheckoutTaxProfileProvider.GetDestination(session).Country, DefaultCheckoutTaxProfileProvider.GetDestination(session).Region));
    }

    private static ContentItem Product(string versionId, ShippingPart part)
    {
        var item = new ContentItem { ContentItemId = "cap", ContentItemVersionId = versionId, ContentType = "StoreProduct" };
        item.Weld(part);

        return item;
    }

    private static OrderLine Line(string contentItemId, string versionId, string variantId, int quantity, decimal unitPrice, ProductType type)
        => new()
        {
            LineId = contentItemId + variantId,
            ContentItemId = contentItemId,
            ContentItemVersionId = versionId,
            VariantId = variantId,
            Title = contentItemId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice,
            ProductType = type,
        };
}
