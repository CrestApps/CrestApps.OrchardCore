using CrestApps.OrchardCore.Inventory.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A bundle's stock is what its parts can make, and selling it holds and sells its parts.
/// </summary>
public sealed class ProductBundlesTests
{
    private static readonly ProductBundleItem[] _kit =
    [
        new() { ContentItemId = "tee", Quantity = 1 },
        new() { ContentItemId = "cap", Quantity = 2 },
    ];

    [Fact]
    public void ABundle_MakesAsManyAsItsScarcestPartAllows()
    {
        var availability = ProductBundles.GetAvailability(_kit, new Dictionary<string, ProductAvailability>
        {
            ["tee"] = new(10, false, 4),
            ["cap"] = new(7, false, 2),
        });

        Assert.Equal(3, availability.Available);
        Assert.Equal(1, availability.LowStockThreshold);
        Assert.False(availability.AllowBackorder);
    }

    [Fact]
    public void APartNotCounted_NeverRunsOut_AndNoCountedPartMeansNoAnswer()
    {
        var onlyTee = ProductBundles.GetAvailability(_kit, new Dictionary<string, ProductAvailability> { ["tee"] = new(5, true, 0) });

        Assert.Equal(5, onlyTee.Available);
        Assert.True(onlyTee.AllowBackorder);
        Assert.Null(ProductBundles.GetAvailability(_kit, new Dictionary<string, ProductAvailability>()));
    }

    [Fact]
    public void APartOutOfStock_SellsTheBundleOut()
    {
        var availability = ProductBundles.GetAvailability(_kit, new Dictionary<string, ProductAvailability>
        {
            ["tee"] = new(10, false, 0),
            ["cap"] = new(1, false, 0),
        });

        Assert.Equal(0, availability.Available);
        Assert.False(availability.CanBuy(1));
    }

    [Fact]
    public void ABundleLine_HoldsItsParts_TimesHowManyBundles()
    {
        var order = new Order
        {
            Lines =
            [
                new OrderLine { ContentItemId = "kit", Quantity = 3, BundleItems = [new() { ContentItemId = "tee", Title = "Tee", Quantity = 1 }, new() { ContentItemId = "cap", Title = "Cap", Quantity = 2 }] },
                new OrderLine { ContentItemId = "mug", VariantId = "large", Quantity = 2 },
            ],
        };

        var requests = InventoryOrderEventHandler.ToRequests(order).ToList();

        Assert.Equal(3, requests.Count);
        Assert.Contains(requests, request => request.ContentItemId == "tee" && request.Quantity == 3 && request.VariantId is null);
        Assert.Contains(requests, request => request.ContentItemId == "cap" && request.Quantity == 6);
        Assert.Contains(requests, request => request.ContentItemId == "mug" && request.VariantId == "large" && request.Quantity == 2);
        Assert.DoesNotContain(requests, request => request.ContentItemId == "kit");
    }

    [Fact]
    public void ABundleLine_ReadsWithWhatToPack()
    {
        var line = new OrderLine { Title = "Starter kit", Quantity = 2, BundleItems = [new() { ContentItemId = "tee", Title = "Tee", Quantity = 1 }, new() { ContentItemId = "cap", Title = "Cap", Quantity = 2 }] };

        Assert.Equal("2 × Tee, 4 × Cap", OrderLineText.DescribeBundle(line));
        Assert.Equal("Starter kit (2 × Tee, 4 × Cap)", OrderLineText.DescribeForPacking(line));
        Assert.Null(OrderLineText.DescribeBundle(new OrderLine { Title = "Mug" }));
        Assert.Equal("Mug", OrderLineText.DescribeForPacking(new OrderLine { Title = "Mug" }));
    }
}
