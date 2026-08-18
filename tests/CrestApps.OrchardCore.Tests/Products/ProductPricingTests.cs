using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class ProductPricingTests
{
    [Fact]
    public async Task Snapshot_UsesProductOwnedCurrency()
    {
        // Arrange
        var resolver = CreateSnapshotResolver(defaultCurrency: "EUR");
        var contentItem = CreateProduct(price: 100m, currency: "usd");

        // Act
        var snapshot = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem) { Currency = "GBP" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(snapshot);
        Assert.Equal("USD", snapshot.Currency);
        Assert.Equal(100m, snapshot.UnitPrice);
    }

    [Fact]
    public async Task Snapshot_FallsBackToTypeDefaultCurrency()
    {
        // Arrange
        var resolver = CreateSnapshotResolver(defaultCurrency: "eur");
        var contentItem = CreateProduct(price: 100m, currency: null);

        // Act
        var snapshot = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("EUR", snapshot.Currency);
    }

    [Fact]
    public async Task Snapshot_ReturnsNull_WhenItemHasNoProductPart()
    {
        // Arrange
        var resolver = CreateSnapshotResolver(defaultCurrency: "USD");
        var contentItem = new ContentItem { ContentType = "Product", ContentItemId = "no-part" };

        // Act
        var snapshot = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task Price_ReturnsListPriceInProductCurrency()
    {
        // Arrange
        var resolver = CreatePriceResolver(defaultCurrency: "USD");
        var contentItem = CreateProduct(price: 25m, currency: "USD");

        // Act
        var price = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem) { Quantity = 3 }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(price);
        Assert.Equal(25m, price.UnitPrice);
        Assert.Equal("USD", price.Currency);
        Assert.Equal(3, price.Quantity);
        Assert.Equal(75m, price.Subtotal);
    }

    [Fact]
    public async Task Price_AllowsMatchingRequestedCurrencyCaseInsensitively()
    {
        // Arrange
        var resolver = CreatePriceResolver(defaultCurrency: "USD");
        var contentItem = CreateProduct(price: 10m, currency: "USD");

        // Act
        var price = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem) { Currency = "usd" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(price);
        Assert.Equal("USD", price.Currency);
    }

    [Fact]
    public async Task Price_RejectsRequestedCurrencyMismatch()
    {
        // Arrange
        var resolver = CreatePriceResolver(defaultCurrency: "USD");
        var contentItem = CreateProduct(price: 10m, currency: "USD");

        // Act
        var price = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem) { Currency = "EUR" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(price);
    }

    [Fact]
    public async Task Price_ReturnsNull_WhenItemIsNotAProduct()
    {
        // Arrange
        var resolver = CreatePriceResolver(defaultCurrency: "USD");
        var contentItem = new ContentItem { ContentType = "Article", ContentItemId = "article-1" };

        // Act
        var price = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(price);
    }

    [Fact]
    public async Task Price_FailsClosed_WhenProductCurrencyIsBlank()
    {
        // Arrange
        var resolver = CreatePriceResolver(defaultCurrency: string.Empty);
        var contentItem = CreateProduct(price: 10m, currency: null);

        // Act
        var price = await resolver.ResolveAsync(new ProductSnapshotContext(contentItem), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PriceResult_Throws_WhenCurrencyIsBlank(string currency)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new PriceResult(10m, currency, 1));
    }

    private static DefaultProductSnapshotResolver CreateSnapshotResolver(string defaultCurrency)
        => new(CreateContentDefinitionManager(defaultCurrency));

    private static DefaultPriceResolver CreatePriceResolver(string defaultCurrency)
        => new(CreateSnapshotResolver(defaultCurrency), NullLogger<DefaultPriceResolver>.Instance);

    private static IContentDefinitionManager CreateContentDefinitionManager(string defaultCurrency)
    {
        var settings = new JsonObject
        {
            [nameof(ProductPartSettings)] = JsonSerializer.SerializeToNode(new ProductPartSettings
            {
                Type = ProductType.Good,
                DefaultCurrency = defaultCurrency,
            }),
        };

        var partDefinition = new ContentTypePartDefinition(
            nameof(ProductPart),
            new ContentPartDefinition(nameof(ProductPart), [], []),
            settings);

        var typeDefinition = new ContentTypeDefinition("Product", "Product", [partDefinition], []);
        partDefinition.ContentTypeDefinition = typeDefinition;

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(x => x.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync(typeDefinition);

        return contentDefinitionManager.Object;
    }

    private static ContentItem CreateProduct(decimal price, string currency)
    {
        var contentItem = new ContentItem
        {
            ContentType = "Product",
            ContentItemId = "product-1",
        };

        contentItem.Apply(nameof(ProductPart), new ProductPart { Price = price, Currency = currency });

        return contentItem;
    }
}
