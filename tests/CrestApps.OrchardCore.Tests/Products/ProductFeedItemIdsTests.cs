using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A feed item id names exactly one product or variant, and checkouts read it back to the product the feed showed:
/// by SKU, by the product's id, or by the product's and the variant's ids, even when the feed cut the id short.
/// </summary>
public sealed class ProductFeedItemIdsTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-feed-ids-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new ContentItemIndexProvider(), new ProductSkuIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("PublishedUtc")
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Owner", column => column.WithLength(255))
            .Column<string>("Author", column => column.WithLength(255))
            .Column<string>("DisplayText", column => column.WithLength(255)));

        await new ProductSkuMigrations { SchemaBuilder = schemaBuilder }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("SKU-1", null, "SKU-1")]
    [InlineData(null, null, "PRODUCT")]
    [InlineData("", "V1", "PRODUCT-V1")]
    [InlineData("SKU-V", "V1", "SKU-V")]
    public void Create_GivesTheSku_ElseTheProductsIdWithTheVariants(string sku, string variantId, string expected)
        => Assert.Equal(expected, ProductFeedItemIds.Create("PRODUCT", variantId, sku));

    [Fact]
    public void Create_CutsALongIdToFiftyCharacters()
    {
        // Act
        var id = ProductFeedItemIds.Create(new string('p', 26), new string('v', 26), sku: null);

        // Assert
        Assert.Equal(ProductFeedItemIds.MaxLength, id.Length);
        Assert.StartsWith(new string('p', 26) + "-", id, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_ReadsEveryIdTheFeedGives_BackToItsProductAndVariant()
    {
        // Arrange
        var simpleWithSku = Product(sku: "HAT-1");
        var simpleWithoutSku = Product();
        var withVariants = Product();
        var colored = new ProductVariant { VariantId = IdGenerator.GenerateId(), Sku = "SHOE-BLK" };
        var plain = new ProductVariant { VariantId = IdGenerator.GenerateId() };

        withVariants.Alter<ProductVariantsPart>(part => part.Variants = [colored, plain]);

        await SaveAsync(simpleWithSku, simpleWithoutSku, withVariants);

        var cases = new (ContentItem Product, ProductVariant Variant, string Sku)[]
        {
            (simpleWithSku, null, "HAT-1"),
            (simpleWithoutSku, null, null),
            (withVariants, colored, colored.Sku),
            (withVariants, plain, null),
        };

        await using var session = _store.CreateSession();
        var resolver = new DefaultProductItemIdResolver(session, Mock.Of<IContentDefinitionManager>());

        foreach (var (product, variant, sku) in cases)
        {
            var itemId = ProductFeedItemIds.Create(product.ContentItemId, variant?.VariantId, sku);

            // Act
            var reference = await resolver.ResolveAsync(itemId, TestContext.Current.CancellationToken);

            // Assert
            Assert.NotNull(reference);
            Assert.Equal(itemId, reference.ItemId);
            Assert.Equal(product.ContentItemId, reference.ContentItemId);
            Assert.Equal(variant?.VariantId, reference.VariantId);
            Assert.Equal(sku, reference.Sku);
        }

        // A product with variants is bought through one of them, so its own id and its own SKU name nothing to buy.
        Assert.Null(await resolver.ResolveAsync(withVariants.ContentItemId, TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync("NO-SUCH-ITEM", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_LeavesOutProductsThatArentPublishedOrSoldHere()
    {
        // Arrange
        var draft = Product(sku: "DRAFT-1", published: false);
        var external = Product(sku: "EXT-1");

        external.Alter<ProductPart>(part => part.ExternalUrl = "https://partner.example.com/item");

        await SaveAsync(draft, external);

        await using var session = _store.CreateSession();
        var resolver = new DefaultProductItemIdResolver(session, Mock.Of<IContentDefinitionManager>());

        // Act & Assert
        Assert.Null(await resolver.ResolveAsync("DRAFT-1", TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync(draft.ContentItemId, TestContext.Current.CancellationToken));
        Assert.Null(await resolver.ResolveAsync("EXT-1", TestContext.Current.CancellationToken));
    }

    private async Task SaveAsync(params ContentItem[] items)
    {
        await using var session = _store.CreateSession();

        foreach (var item in items)
        {
            await session.SaveAsync(item, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ContentItem Product(string sku = null, bool published = true)
    {
        var item = new ContentItem
        {
            ContentItemId = IdGenerator.GenerateId(),
            ContentItemVersionId = IdGenerator.GenerateId(),
            ContentType = "Product",
            DisplayText = "Product",
            Published = published,
            Latest = true,
        };

        item.Alter<ProductPart>(part =>
        {
            part.Price = 20m;
            part.Currency = "USD";
            part.Sku = sku;
        });

        return item;
    }
}
