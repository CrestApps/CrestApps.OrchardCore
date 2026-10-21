using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class ProductPurchasabilityTests
{
    [Theory]
    [InlineData(ProductSalesMode.Purchasable, false, true)]
    [InlineData(ProductSalesMode.Purchasable, true, true)]
    [InlineData(ProductSalesMode.ListingOnly, true, false)]
    [InlineData(ProductSalesMode.PerItem, true, true)]
    [InlineData(ProductSalesMode.PerItem, false, false)]
    public void CanBePurchased_CombinesTheTypeModeWithTheItemFlag(ProductSalesMode mode, bool itemFlag, bool expected)
    {
        var part = new ProductPart { IsPurchasable = itemFlag };

        Assert.Equal(expected, part.CanBePurchased(new ProductPartSettings { SalesMode = mode }));
    }

    [Fact]
    public void CanBePurchased_DefaultsToPurchasable_WhenTheTypeHasNoSettings()
    {
        // Product types created before listing existed have no sales mode and must keep selling.
        Assert.True(new ProductPart { IsPurchasable = false }.CanBePurchased(settings: null));
    }

    [Fact]
    public void GetPriceDisplay_HonorsTheItemOverride_OnlyWhenTheTypeAllowsIt()
    {
        var part = new ProductPart { PriceDisplay = ProductPriceDisplay.Hidden };

        Assert.Equal(ProductPriceDisplay.Show, part.GetPriceDisplay(new ProductPartSettings { PriceDisplay = ProductPriceDisplay.Show }));
        Assert.Equal(ProductPriceDisplay.Hidden, part.GetPriceDisplay(new ProductPartSettings { PriceDisplay = ProductPriceDisplay.Show, AllowPriceDisplayOverride = true }));
    }

    [Fact]
    public void RequiresPrice_IsFalse_ForAListedProductWhosePriceIsNotShown()
    {
        var settings = new ProductPartSettings { SalesMode = ProductSalesMode.ListingOnly, PriceDisplay = ProductPriceDisplay.OnRequest };

        Assert.False(new ProductPart().RequiresPrice(settings));
        Assert.True(new ProductPart().RequiresPrice(new ProductPartSettings { SalesMode = ProductSalesMode.ListingOnly }));
        Assert.True(new ProductPart().RequiresPrice(new ProductPartSettings { SalesMode = ProductSalesMode.Purchasable, PriceDisplay = ProductPriceDisplay.Hidden }));
    }

    [Theory]
    [InlineData(ProductSalesMode.ListingOnly, true)]
    [InlineData(ProductSalesMode.PerItem, false)]
    public async Task Snapshot_RefusesAProductThatCannotBeBought(ProductSalesMode mode, bool itemFlag)
    {
        // Every checkout path resolves its snapshot here, so this is what stops a crafted request from buying
        // something the merchant only lists.
        var resolver = new DefaultProductSnapshotResolver(CreateDefinitions(new ProductPartSettings { SalesMode = mode, DefaultCurrency = "USD" }), new TestClock(DateTime.UtcNow));
        var item = CreateProduct(new ProductPart { Price = 15599m, IsPurchasable = itemFlag });

        var snapshot = await resolver.ResolveAsync(new ProductSnapshotContext(item), TestContext.Current.CancellationToken);

        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Snapshot_ResolvesAPerItemProductThatIsForSale()
    {
        var resolver = new DefaultProductSnapshotResolver(CreateDefinitions(new ProductPartSettings { SalesMode = ProductSalesMode.PerItem, DefaultCurrency = "USD" }), new TestClock(DateTime.UtcNow));
        var item = CreateProduct(new ProductPart { Price = 10999m, IsPurchasable = true });

        var snapshot = await resolver.ResolveAsync(new ProductSnapshotContext(item), TestContext.Current.CancellationToken);

        Assert.NotNull(snapshot);
        Assert.Equal(10999m, snapshot.UnitPrice);
    }

    [Fact]
    public async Task Snapshot_RefusesAProductWithNoPriceAndNoPriceList()
    {
        var resolver = new DefaultProductSnapshotResolver(CreateDefinitions(new ProductPartSettings { DefaultCurrency = "USD" }), new TestClock(DateTime.UtcNow));
        var item = CreateProduct(new ProductPart { Price = null });

        var snapshot = await resolver.ResolveAsync(new ProductSnapshotContext(item), TestContext.Current.CancellationToken);

        Assert.Null(snapshot);
    }

    private static ContentItem CreateProduct(ProductPart part)
    {
        var item = new ContentItem { ContentType = "Vehicle", ContentItemId = "vehicle-1" };
        item.Apply(nameof(ProductPart), part);

        return item;
    }

    private static IContentDefinitionManager CreateDefinitions(ProductPartSettings settings)
    {
        var partDefinition = new ContentTypePartDefinition(
            nameof(ProductPart),
            new ContentPartDefinition(nameof(ProductPart), [], []),
            new JsonObject { [nameof(ProductPartSettings)] = JsonSerializer.SerializeToNode(settings) });

        var typeDefinition = new ContentTypeDefinition("Vehicle", "Vehicle", [partDefinition], []);
        partDefinition.ContentTypeDefinition = typeDefinition;

        var manager = new Mock<IContentDefinitionManager>();
        manager.Setup(x => x.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync(typeDefinition);

        return manager.Object;
    }
}
