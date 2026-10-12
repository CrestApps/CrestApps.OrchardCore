using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Products;

public class ProductTaxableItemProviderTests
{
    [Fact]
    public void CanCreate_ReturnsFalse_WhenContentItemHasNoTaxationPart()
    {
        var provider = CreateProvider(ProductType.Good);
        var contentItem = CreateProductContentItem(price: 100, taxationPart: null);

        Assert.False(provider.CanCreate(contentItem));
    }

    [Fact]
    public void CanCreate_ReturnsTrue_WhenProductHasTaxationPart()
    {
        var provider = CreateProvider(ProductType.Good);
        var contentItem = CreateProductContentItem(price: 100, taxationPart: new JsonObject { ["Taxable"] = true });

        Assert.True(provider.CanCreate(contentItem));
    }

    [Fact]
    public async Task CreateAsync_ReturnsNull_WhenNotTaxable()
    {
        var provider = CreateProvider(ProductType.Good);
        var contentItem = CreateProductContentItem(price: 100, taxationPart: new JsonObject { ["Taxable"] = false });

        var item = await provider.CreateAsync(contentItem, TestContext.Current.CancellationToken);

        Assert.Null(item);
    }

    [Fact]
    public async Task CreateAsync_ReadsPriceAndClassificationFromParts()
    {
        var provider = CreateProvider(ProductType.Good);
        var contentItem = CreateProductContentItem(
            price: 149.99m,
            taxationPart: new JsonObject
            {
                ["Taxable"] = true,
                ["TaxCategoryCode"] = "Electronics",
                ["TaxClassificationCode"] = "Television",
                ["ExternalTaxCode"] = "EX-100",
            });

        var item = await provider.CreateAsync(contentItem, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal(contentItem.ContentItemId, item.Id);
        Assert.Equal(149.99m, item.UnitPrice);
        Assert.Equal("USD", item.Currency);
        Assert.Equal(1m, item.Quantity);
        Assert.Equal("Electronics", item.TaxCategoryCode);
        Assert.Equal("Television", item.TaxClassificationCode);
        Assert.Equal("EX-100", item.ExternalTaxCode);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenProductHasNoCurrency()
    {
        var provider = CreateProvider(ProductType.Good, defaultCurrency: null);
        var contentItem = CreateProductContentItem(price: 100, taxationPart: new JsonObject { ["Taxable"] = true });

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await provider.CreateAsync(contentItem, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ProductType.Good, TaxableItemKind.Physical)]
    [InlineData(ProductType.Service, TaxableItemKind.Service)]
    [InlineData(ProductType.Digital, TaxableItemKind.Digital)]
    [InlineData(ProductType.Undefined, TaxableItemKind.Physical)]
    public async Task CreateAsync_MapsProductTypeToTaxableItemKind(ProductType productType, TaxableItemKind expectedKind)
    {
        var provider = CreateProvider(productType);
        var contentItem = CreateProductContentItem(price: 50, taxationPart: new JsonObject { ["Taxable"] = true });

        var item = await provider.CreateAsync(contentItem, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal(expectedKind, item.Kind);
    }

    [Fact]
    public async Task CreateAsync_DefaultsToTaxable_WhenFlagAbsent()
    {
        var provider = CreateProvider(ProductType.Good);
        var contentItem = CreateProductContentItem(price: 100, taxationPart: new JsonObject());

        var item = await provider.CreateAsync(contentItem, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
    }

    [Fact]
    public void Order_RunsBeforeGenericProvider()
    {
        var provider = CreateProvider(ProductType.Good);

        Assert.True(provider.Order < 0);
    }

    private static ProductTaxableItemProvider CreateProvider(ProductType productType, string defaultCurrency = "USD")
    {
        var settings = new JsonObject
        {
            [nameof(ProductPartSettings)] = JsonSerializer.SerializeToNode(new ProductPartSettings { Type = productType, DefaultCurrency = defaultCurrency }),
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

        return new ProductTaxableItemProvider(new DefaultProductSnapshotResolver(contentDefinitionManager.Object));
    }

    private static ContentItem CreateProductContentItem(decimal price, JsonObject taxationPart)
    {
        var contentItem = new ContentItem
        {
            ContentType = "Product",
            ContentItemId = "product-1",
        };

        contentItem.Apply(nameof(ProductPart), new ProductPart { Price = price });

        if (taxationPart is not null)
        {
            contentItem.Content["TaxationPart"] = taxationPart;
        }

        return contentItem;
    }
}
