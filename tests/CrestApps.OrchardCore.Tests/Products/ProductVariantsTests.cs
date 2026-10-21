using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A product sold in variants is bought as one of them: the variant's SKU and price reach the cart and the order,
/// and listings filter on its options and show "From" its lowest price.
/// </summary>
public sealed class ProductVariantsTests
{
    [Fact]
    public void EveryCombinationOfValues_IsAVariant_AndAKeptCombinationKeepsItsSkuAndPrice()
    {
        var ids = 0;
        string NewId() => $"v{++ids}";

        var first = ProductVariants.Combine(
            [new ProductOption { Name = "Size", Values = ["S", "M"] }, new ProductOption { Name = "Color", Values = ["Red", "Blue"] }],
            existing: [],
            NewId);

        Assert.Equal(4, first.Count);
        Assert.Equal(4, first.Select(variant => variant.VariantId).Distinct().Count());

        var redMedium = first.Single(variant => variant.Options["Size"] == "M" && variant.Options["Color"] == "Red");
        redMedium.Sku = "SHIRT-M-RED";
        redMedium.Price = 25m;

        var redSmallId = first.Single(variant => variant.Options["Size"] == "S" && variant.Options["Color"] == "Red").VariantId;

        // M is dropped and L added: the S variants stay, the M ones go, and the L ones are new.
        var second = ProductVariants.Combine(
            [new ProductOption { Name = "Size", Values = ["S", "L", " "] }, new ProductOption { Name = "Color", Values = ["red", "Blue"] }],
            first,
            NewId);

        Assert.Equal(4, second.Count);
        Assert.DoesNotContain(second, variant => variant.Sku == "SHIRT-M-RED");
        Assert.Contains(second, variant => variant.VariantId == redSmallId && variant.Options["Color"] == "red");
        Assert.Equal(2, second.Count(variant => variant.VariantId is "v5" or "v6"));
    }

    [Fact]
    public void NoValues_MakeNoVariants()
        => Assert.Empty(ProductVariants.Combine([new ProductOption { Name = "Size", Values = [] }], [], () => "x"));

    [Fact]
    public void WhenTheTypeGainsAnOption_EachVariantCarriesOnAsItsFirstCombination()
    {
        var ids = 0;
        string NewId() => $"v{++ids}";

        var sizes = ProductVariants.Combine([new ProductOption { Name = "Size", Values = ["S", "M"] }], [], NewId);
        var medium = sizes.Single(variant => variant.Options["Size"] == "M");
        medium.Sku = "CAP-M";
        medium.Price = 25m;

        var withColors = ProductVariants.Combine(
            [new ProductOption { Name = "Size", Values = ["S", "M"] }, new ProductOption { Name = "Color", Values = ["Red", "Blue"] }],
            sizes,
            NewId);

        Assert.Equal(4, withColors.Count);

        // M becomes M / Red, with its SKU and price; M / Blue is new.
        var mediumRed = withColors.Single(variant => variant.Options["Size"] == "M" && variant.Options["Color"] == "Red");
        Assert.Equal(medium.VariantId, mediumRed.VariantId);
        Assert.Equal("CAP-M", mediumRed.Sku);
        Assert.Equal(25m, mediumRed.Price);
        Assert.Null(withColors.Single(variant => variant.Options["Size"] == "M" && variant.Options["Color"] == "Blue").Sku);
    }

    [Fact]
    public void WhenTheTypeLosesAnOption_TheFirstVariantOfEachValueCarriesOn()
    {
        var ids = 0;
        string NewId() => $"v{++ids}";

        var both = ProductVariants.Combine(
            [new ProductOption { Name = "Size", Values = ["S", "M"] }, new ProductOption { Name = "Color", Values = ["Red", "Blue"] }],
            [],
            NewId);

        var mediumRed = both.Single(variant => variant.Options["Size"] == "M" && variant.Options["Color"] == "Red");
        mediumRed.Sku = "CAP-M-RED";

        var sizesOnly = ProductVariants.Combine([new ProductOption { Name = "Size", Values = ["S", "M"] }], both, NewId);

        Assert.Equal(2, sizesOnly.Count);
        Assert.Equal("CAP-M-RED", sizesOnly.Single(variant => variant.Options["Size"] == "M").Sku);
        Assert.All(sizesOnly, variant => Assert.Single(variant.Options));
    }

    [Fact]
    public void AnExactMatch_IsNeverTakenByAVariantThatOnlyAgrees()
    {
        var ids = 0;
        string NewId() => $"v{++ids}";

        // The old "M" variant agrees with M / Red, but M / Red already has its own variant.
        ProductVariant[] existing =
        [
            new() { VariantId = "old-m", Options = new(StringComparer.OrdinalIgnoreCase) { ["Size"] = "M" }, Sku = "OLD-M" },
            new() { VariantId = "m-red", Options = new(StringComparer.OrdinalIgnoreCase) { ["Size"] = "M", ["Color"] = "Red" }, Sku = "M-RED" },
        ];

        var variants = ProductVariants.Combine(
            [new ProductOption { Name = "Size", Values = ["M"] }, new ProductOption { Name = "Color", Values = ["Red", "Blue"] }],
            existing,
            NewId);

        Assert.Equal("M-RED", variants.Single(variant => variant.Options["Color"] == "Red").Sku);
        Assert.Equal("OLD-M", variants.Single(variant => variant.Options["Color"] == "Blue").Sku);
    }

    [Fact]
    public async Task AVariantProduct_IsSoldAsAnEnabledVariant_WithItsSkuPriceAndTitle()
    {
        var resolver = CreateSnapshotResolver();
        var shirt = CreateShirt();

        Assert.Null(await resolver.ResolveAsync(new ProductSnapshotContext(shirt), TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync(new ProductSnapshotContext(shirt) { VariantId = "red-l" }, TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync(new ProductSnapshotContext(shirt) { VariantId = "missing" }, TestContext.Current.CancellationToken));

        var redSmall = await resolver.ResolveAsync(new ProductSnapshotContext(shirt) { VariantId = "red-s" }, TestContext.Current.CancellationToken);

        Assert.Equal("SHIRT-RED-S", redSmall.Sku);
        Assert.Equal(20m, redSmall.UnitPrice);
        Assert.Equal("Shirt (Red / S)", redSmall.Title);
        Assert.Equal("red-s", redSmall.VariantId);

        // A variant without its own SKU or price sells under the product's.
        var blueSmall = await resolver.ResolveAsync(new ProductSnapshotContext(shirt) { VariantId = "blue-s" }, TestContext.Current.CancellationToken);

        Assert.Equal("SHIRT", blueSmall.Sku);
        Assert.Equal(15m, blueSmall.UnitPrice);
    }

    [Fact]
    public async Task AVariant_IsPricedAtItsOwnPrice_EvenWhenTheProductListsPrices()
    {
        var shirt = CreateShirt();
        shirt.Apply(nameof(ProductPricePart), new ProductPricePart
        {
            Prices = [new ProductPrice { PriceId = "list", Amount = 99m, Currency = "USD", IsDefault = true }],
        });

        var resolver = new DefaultPriceResolver(CreateSnapshotResolver(), new TestClock(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)), NullLogger<DefaultPriceResolver>.Instance);
        var price = await resolver.ResolveAsync(new ProductSnapshotContext(shirt) { VariantId = "red-s", Quantity = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(20m, price.UnitPrice);
        Assert.Equal(40m, price.Subtotal);
    }

    [Fact]
    public async Task TheSearchRecord_CarriesTheOptionsOnSale_TheSkus_AndThePriceRange()
    {
        var shirt = CreateShirt();
        var document = new ProductSearchDocument
        {
            Price = 15m,
            PriceDisplay = ProductPriceDisplay.Show,
            Attributes = [new ProductAttributeValue { Key = ProductAttributeKeys.Price, Number = 15m, Text = "15" }],
        };

        await new ProductVariantsSearchDocumentContributor(new TestClock(DateTime.UtcNow)).ContributeAsync(shirt, document);

        Assert.Equal(15m, document.Price);
        Assert.Equal(20m, document.MaxPrice);
        Assert.Equal("products/red.jpg", document.ImagePath);

        // The disabled Red / L variant does not make the product a size L.
        var sizes = document.Attributes.Where(value => value.Key == ProductVariants.AttributeKey("Size")).Select(value => value.Text);
        Assert.Equal(["S"], sizes);
        Assert.Equal(2, document.Attributes.Count(value => value.Key == ProductVariants.AttributeKey("Color")));
        Assert.Contains(document.Attributes, value => value.Key == ProductVariantsSearchDocumentContributor.SkuKey && value.Text == "SHIRT-RED-S" && value.Searchable);
        Assert.Equal([15m, 20m], document.Attributes.Where(value => value.Key == ProductAttributeKeys.Price).Select(value => value.Number.Value).Order());
    }

    [Fact]
    public async Task AHiddenPrice_StaysOutOfTheSearchRecord()
    {
        var document = new ProductSearchDocument { PriceDisplay = ProductPriceDisplay.Hidden };

        await new ProductVariantsSearchDocumentContributor(new TestClock(DateTime.UtcNow)).ContributeAsync(CreateShirt(), document);

        Assert.Null(document.Price);
        Assert.Null(document.MaxPrice);
    }

    private static ContentItem CreateShirt()
    {
        var shirt = new ContentItem { ContentType = "Shirt", ContentItemId = "shirt", DisplayText = "Shirt" };

        shirt.Apply(nameof(ProductPart), new ProductPart { Price = 15m, Currency = "USD", Sku = "SHIRT" });
        shirt.Apply(nameof(ProductVariantsPart), new ProductVariantsPart
        {
            Options =
            [
                new ProductOption { Name = "Color", Values = ["Red", "Blue"] },
                new ProductOption { Name = "Size", Values = ["S", "L"] },
            ],
            Variants =
            [
                Variant("red-s", "Red", "S", sku: "SHIRT-RED-S", price: 20m, image: "products/red.jpg"),
                Variant("blue-s", "Blue", "S"),
                Variant("red-l", "Red", "L", enabled: false),
            ],
        });

        return shirt;
    }

    private static ProductVariant Variant(string id, string color, string size, string sku = null, decimal? price = null, string image = null, bool enabled = true)
        => new()
        {
            VariantId = id,
            Options = new(StringComparer.OrdinalIgnoreCase) { ["Color"] = color, ["Size"] = size },
            Sku = sku,
            Price = price,
            ImagePath = image,
            Enabled = enabled,
        };

    private static DefaultProductSnapshotResolver CreateSnapshotResolver()
    {
        var settings = new JsonObject
        {
            [nameof(ProductPartSettings)] = JsonSerializer.SerializeToNode(new ProductPartSettings { Type = ProductType.Good, DefaultCurrency = "USD" }),
        };

        var partDefinition = new ContentTypePartDefinition(nameof(ProductPart), new ContentPartDefinition(nameof(ProductPart), [], []), settings);
        var typeDefinition = new ContentTypeDefinition("Shirt", "Shirt", [partDefinition], []);
        partDefinition.ContentTypeDefinition = typeDefinition;

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync(typeDefinition);

        return new DefaultProductSnapshotResolver(contentDefinitionManager.Object, new TestClock(DateTime.UtcNow));
    }
}
