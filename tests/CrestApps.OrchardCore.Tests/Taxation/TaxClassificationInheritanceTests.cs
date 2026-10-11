using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.ContentManagement;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxClassificationInheritanceTests
{
    private static ContentItem CreateProduct(TaxationPart part, decimal price = 100m)
    {
        var contentItem = new ContentItem
        {
            ContentType = "Product",
            ContentItemId = "product-1",
        };

        contentItem.Weld(nameof(TaxationPart), part);

        contentItem.Content["ProductPart"] = new JsonObject
        {
            ["Price"] = JsonValue.Create(price),
        };

        return contentItem;
    }

    [Fact]
    public async Task Item_WithoutOwnCategory_InheritsClassificationFromProvider()
    {
        var provider = new ContentItemTaxableItemProvider(
        [
            new FakeClassificationProvider(new TaxClassification
            {
                TaxCategoryCode = "Tobacco",
                TaxClassificationCode = "Cigarettes",
                ExternalTaxCode = "TX-99",
            }),
        ]);

        var product = CreateProduct(new TaxationPart { Taxable = true });

        var item = await provider.CreateAsync(product, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal("Tobacco", item.TaxCategoryCode);
        Assert.Equal("Cigarettes", item.TaxClassificationCode);
        Assert.Equal("TX-99", item.ExternalTaxCode);
    }

    [Fact]
    public async Task Item_WithOwnCategory_OverridesProvider()
    {
        var provider = new ContentItemTaxableItemProvider(
        [
            new FakeClassificationProvider(new TaxClassification
            {
                TaxCategoryCode = "Tobacco",
                TaxClassificationCode = "Cigarettes",
            }),
        ]);

        var product = CreateProduct(new TaxationPart
        {
            Taxable = true,
            TaxCategoryCode = "Electronics",
            TaxClassificationCode = "Television",
        });

        var item = await provider.CreateAsync(product, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal("Electronics", item.TaxCategoryCode);
        Assert.Equal("Television", item.TaxClassificationCode);
    }

    [Fact]
    public async Task Providers_AreConsultedInOrder_FirstWithCategoryWins()
    {
        var provider = new ContentItemTaxableItemProvider(
        [
            new FakeClassificationProvider(classification: null, order: 5),
            new FakeClassificationProvider(new TaxClassification { TaxCategoryCode = "Tobacco" }, order: 10),
            new FakeClassificationProvider(new TaxClassification { TaxCategoryCode = "Alcohol" }, order: 20),
        ]);

        var product = CreateProduct(new TaxationPart { Taxable = true });

        var item = await provider.CreateAsync(product, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal("Tobacco", item.TaxCategoryCode);
    }

    [Fact]
    public async Task Item_KeepsOwnClassificationCode_WhenInheritingOnlyCategory()
    {
        var provider = new ContentItemTaxableItemProvider(
        [
            new FakeClassificationProvider(new TaxClassification
            {
                TaxCategoryCode = "Tobacco",
                TaxClassificationCode = "Cigarettes",
            }),
        ]);

        // The item declares a classification code but no category code; the category is inherited while the
        // explicit classification code is preserved.
        var product = CreateProduct(new TaxationPart
        {
            Taxable = true,
            TaxClassificationCode = "Premium",
        });

        var item = await provider.CreateAsync(product, TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal("Tobacco", item.TaxCategoryCode);
        Assert.Equal("Premium", item.TaxClassificationCode);
    }

    [Fact]
    public async Task NonTaxableItem_IsNeverResolved_EvenWithProvider()
    {
        var provider = new ContentItemTaxableItemProvider(
        [
            new FakeClassificationProvider(new TaxClassification { TaxCategoryCode = "Tobacco" }),
        ]);

        var product = CreateProduct(new TaxationPart { Taxable = false });

        var item = await provider.CreateAsync(product, TestContext.Current.CancellationToken);

        Assert.Null(item);
    }

    private sealed class FakeClassificationProvider : ITaxClassificationProvider
    {
        private readonly TaxClassification _classification;

        public FakeClassificationProvider(TaxClassification classification, int order = 0)
        {
            _classification = classification;
            Order = order;
        }

        public int Order { get; }

        public ValueTask<TaxClassification> GetClassificationAsync(ContentItem contentItem, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(_classification);
    }
}
