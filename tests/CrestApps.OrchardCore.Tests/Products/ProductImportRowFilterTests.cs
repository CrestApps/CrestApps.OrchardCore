using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Products.ContentTransfer;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Migrations;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using Xunit;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A product import updates the product a row names by SKU, its own or a variant's, and skips a SKU the file already
/// used.
/// </summary>
public sealed class ProductImportRowFilterTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"crestapps-product-sku-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_path};Pooling=False"));
        _store.RegisterIndexes([new ProductSkuIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = _store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync([]);
        await new ProductSkuMigrations(session, definitions.Object) { SchemaBuilder = new SchemaBuilder(_store.Configuration, transaction) }.CreateAsync();
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ARowMatchingASku_UpdatesThatProduct_AndARepeatedSkuIsSkipped()
    {
        await SaveAsync("cap", "CAP-1");
        await SaveAsync("tee", sku: null, variantSku: "TEE-S");

        var table = new DataTable();
        table.Columns.Add("ContentItemId");
        table.Columns.Add("ProductPart_Sku");
        table.Columns.Add("ProductVariantsPart_Variants");
        var cap = Row(table, sku: "cap-1");
        var tee = Row(table, variants: """[{"options":{"Size":"S"},"sku":"TEE-S"}]""");
        var fresh = Row(table, sku: "NEW-1");
        var repeat = Row(table, sku: "NEW-1");

        await using var session = _store.CreateSession();
        var filter = new ProductImportRowFilter(session, new PassThroughStringLocalizer<ProductImportRowFilter>());
        var rows = new[] { cap, tee, fresh, repeat }.Select((row, i) => new ContentImportRowFilterContext { Row = row, Columns = table.Columns, RowIndex = i + 1 }).ToList();

        Assert.True(await filter.InitializeAsync(new ContentImportRowFilterInitContext { ContentTypeDefinition = Type() }));
        await filter.PrepareBatchAsync(new ContentImportRowFilterBatchContext { ContentTypeDefinition = Type(), Rows = rows, CancellationToken = TestContext.Current.CancellationToken });

        var skipped = new List<bool>();

        foreach (var row in rows)
        {
            skipped.Add(await filter.ShouldSkipRowAsync(row));
        }

        Assert.Equal([false, false, false, true], skipped);
        Assert.Equal("cap", cap["ContentItemId"]);
        Assert.Equal("tee", tee["ContentItemId"]);
        Assert.True(string.IsNullOrEmpty(fresh["ContentItemId"]?.ToString()));
        Assert.Contains("NEW-1", rows[3].SkipReason);
    }

    [Fact]
    public async Task AMatchWithoutAContentItemIdColumn_IsSkipped_SayingHowToUpdateIt()
    {
        await SaveAsync("cap", "CAP-1");

        var table = new DataTable();
        table.Columns.Add("ProductPart_Sku");
        var row = table.NewRow();
        row["ProductPart_Sku"] = "CAP-1";

        await using var session = _store.CreateSession();
        var filter = new ProductImportRowFilter(session, new PassThroughStringLocalizer<ProductImportRowFilter>());
        var context = new ContentImportRowFilterContext { Row = row, Columns = table.Columns, RowIndex = 1 };

        await filter.InitializeAsync(new ContentImportRowFilterInitContext { ContentTypeDefinition = Type() });
        await filter.PrepareBatchAsync(new ContentImportRowFilterBatchContext { ContentTypeDefinition = Type(), Rows = [context], CancellationToken = TestContext.Current.CancellationToken });

        Assert.True(await filter.ShouldSkipRowAsync(context));
        Assert.Contains("ContentItemId", context.SkipReason);
    }

    private async Task SaveAsync(string id, string sku, string variantSku = null)
    {
        var item = new ContentItem { ContentItemId = id, ContentItemVersionId = id + "-v1", ContentType = "StoreProduct", Latest = true, Published = true };
        item.Alter<ProductPart>(part => part.Sku = sku);

        if (variantSku is not null)
        {
            item.Alter<ProductVariantsPart>(part => part.Variants = [new ProductVariant { VariantId = "v1", Sku = variantSku, Options = new(StringComparer.OrdinalIgnoreCase) { ["Size"] = "S" } }]);
        }

        await using var session = _store.CreateSession();
        await session.SaveAsync(item, cancellationToken: TestContext.Current.CancellationToken);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static DataRow Row(DataTable table, string sku = null, string variants = null)
    {
        var row = table.NewRow();
        row["ProductPart_Sku"] = sku;
        row["ProductVariantsPart_Variants"] = variants;

        return row;
    }

    private static ContentTypeDefinition Type()
    {
        var type = new ContentTypeDefinition("StoreProduct", "Store Product",
        [
            new ContentTypePartDefinition("ProductPart", new ContentPartDefinition("ProductPart", [], []), []),
            new ContentTypePartDefinition("ProductVariantsPart", new ContentPartDefinition("ProductVariantsPart", [], []), []),
        ], []);

        foreach (var part in type.Parts)
        {
            part.ContentTypeDefinition = type;
        }

        return type;
    }
}
