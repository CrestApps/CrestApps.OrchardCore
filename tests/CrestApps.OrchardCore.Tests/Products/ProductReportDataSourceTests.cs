using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Reports.DataSources;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.DataSources;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// The products data source reads real content items and product search records from a SQLite store, so the content item
/// index queries, the choice of the version each principal reads, the content type permissions, the fields, and the
/// push-down of join keys and content types are all exercised end to end.
/// </summary>
public sealed class ProductReportDataSourceTests : IAsyncLifetime
{
    private const string ProductContentType = "Product";
    private const string PlanContentType = "Plan";
    private const string SecretContentType = "Secret";
    private const string ArticleContentType = "Article";

    private static readonly ClaimsPrincipal _user = ReportDesignerPrincipals.User("u1", "Merchandiser");
    private static readonly DateTime _start = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    // The viewer may view products and plans, and edit plans: it reads the published products and the latest plans.
    private static readonly string[] _viewer = ["ViewContent:" + ProductContentType, "ViewContent:" + PlanContentType, "EditContent:" + PlanContentType];

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"product-reports-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ContentItemIndexProvider(), new ProductSearchIndexProvider()]);

        await _store.InitializeAsync(cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await CreateIndexTablesAsync(schemaBuilder);
            await transaction.CommitAsync(cancellationToken);
        }

        // Saved one at a time, so the index rows are in this order: the last one is the newest.
        ContentItem[] items =
        [
            Mug("mug-a", published: false, latest: false, title: "Mug (old)", sku: "MUG-0"),
            Mug("mug-b", published: true, latest: false, title: "Mug", sku: "MUG-1"),
            Mug("mug-c", published: false, latest: true, title: "Mug (draft)", sku: "MUG-2"),
            Kit(),
            Plan("gold", "gold-x", published: true, latest: false, title: "Gold"),
            Plan("gold", "gold-y", published: false, latest: true, title: "Gold (draft)"),
            Plan("silver", "silver-a", published: false, latest: true, title: "Silver"),
            Item("hidden", "hidden-a", SecretContentType, "Hidden", published: true, latest: true, day: 7, item => item.Alter<ProductPart>(part => part.Price = 1m)),
            Item("post", "post-a", ArticleContentType, "Post", published: true, latest: true, day: 8),
        ];

        foreach (var item in items)
        {
            await using var session = _store.CreateSession();

            await session.SaveAsync(item, cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(SearchRecord("mug", ProductContentType, "Mug", new ProductAttributeValue { Key = "Make", Text = "acme", Label = "Acme" }, new ProductAttributeValue { Key = "Capacity", Number = 0.3m }), cancellationToken: cancellationToken);
            await session.SaveAsync(SearchRecord("hidden", SecretContentType, "Hidden", new ProductAttributeValue { Key = "Make", Text = "secret", Label = "Secret" }), cancellationToken: cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task WithoutViewContent_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, "EditContent:" + ArticleContentType, "ViewContent:" + ArticleContentType);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ProductSource.Products, context, TestContext.Current.CancellationToken);
        var products = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ProductSource.Products), TestContext.Current.CancellationToken);
        var attributes = await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ProductSource.Attributes), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(products.Rows);
        Assert.Empty(attributes.Rows);
    }

    [Fact]
    public async Task WithoutAUser_NothingIsExposed()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);
        var context = new DataSourceContext();
        var query = Query(CommerceReportDataSets.ProductSource.Products);
        query.Context = context;

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var schema = await source.GetSchemaAsync(CommerceReportDataSets.ProductSource.Products, context, TestContext.Current.CancellationToken);
        var table = await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(dataSets);
        Assert.Null(schema);
        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task ViewingOneType_ReadsThePublishedProductsOfThatTypeOnly()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, "ViewContent:" + PlanContentType);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ProductSource.Products), TestContext.Current.CancellationToken));

        // Assert
        // A viewer who may not edit plans reads their published versions, so the never-published plan is left out.
        var gold = Assert.Single(rows);
        Assert.Equal("gold", gold[CommerceReportDataSets.ContentItemIdField]);
        Assert.Equal("gold-x", gold["ContentItemVersionId"]);
        Assert.Equal(true, gold["Published"]);
    }

    [Fact]
    public async Task Schema_DeclaresKeysAndReferences_AndNeverExposesPrivateDetails()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);
        var context = new DataSourceContext { User = _user };

        // Act
        var dataSets = await source.GetDataSetsAsync(context, TestContext.Current.CancellationToken);
        var products = await source.GetSchemaAsync(CommerceReportDataSets.ProductSource.Products, context, TestContext.Current.CancellationToken);
        var variants = await source.GetSchemaAsync(CommerceReportDataSets.ProductSource.Variants, context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [
                CommerceReportDataSets.ProductSource.Products,
                CommerceReportDataSets.ProductSource.Variants,
                CommerceReportDataSets.ProductSource.Prices,
                CommerceReportDataSets.ProductSource.BundleItems,
                CommerceReportDataSets.ProductSource.Attributes,
            ],
            dataSets.Select(dataSet => dataSet.Name));

        // The content item index has no date that is never empty, so no date is pushed down.
        Assert.Null(products.DataSet.DefaultDateField);
        Assert.True(products.FindField(CommerceReportDataSets.ContentItemIdField).IsIdentifier);
        Assert.True(products.FindField(CommerceReportDataSets.ContentItemIdField).IsKeyFilterable);
        Assert.Equal(ReportsConstants.UsersDataSource, Assert.Single(products.FindField("Owner").References).Source);
        Assert.Equal(DataFieldType.Decimal, products.FindField("Price").Type);
        Assert.False(string.IsNullOrEmpty(products.FindField("Price").Description));
        Assert.Equal(DataFieldType.Text, products.FindField("Currency").Type);
        Assert.DoesNotContain(products.Fields, field => field.Name is "ExternalUrl" or "AddOns" or "RoleDiscounts" or "PriceTiers");

        Assert.True(variants.FindField(CommerceReportDataSets.VariantIdField).IsIdentifier);
        Assert.True(variants.FindField(ProductEntryReportDataSet<ProductVariant>.ProductIdField).IsKeyFilterable);
        var product = Assert.Single(variants.FindField(ProductEntryReportDataSet<ProductVariant>.ProductIdField).References);
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, product.Source);
        Assert.Equal(CommerceReportDataSets.ProductSource.Products, product.DataSet);
        Assert.Equal(CommerceReportDataSets.ContentItemIdField, product.Field);
        Assert.DoesNotContain(variants.Fields, field => field.Name is "ImagePath");
    }

    [Fact]
    public async Task Query_Products_GivesOneRowPerProduct_NewestFirst()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ProductSource.Products), TestContext.Current.CancellationToken));

        // Assert
        // The published mug and kit, and the latest gold and silver plans: one row each, never the hidden type or the article.
        Assert.Equal(["silver", "gold", "kit", "mug"], rows.Select(row => row[CommerceReportDataSets.ContentItemIdField]));

        var mug = rows[3];
        Assert.Equal("mug-b", mug["ContentItemVersionId"]);
        Assert.Equal(ProductContentType, mug["ContentType"]);
        Assert.Equal("Mug", mug["Title"]);
        Assert.Equal("MUG-1", mug["Sku"]);
        Assert.Equal("Good", mug["ProductType"]);
        Assert.Equal(true, mug["IsPurchasable"]);
        Assert.Equal(true, mug["Featured"]);
        Assert.Equal("USD", mug["Currency"]);
        Assert.Equal(20m, mug["Price"]);
        Assert.Equal(25m, mug["CompareAtPrice"]);
        Assert.Equal(15m, mug["SalePrice"]);
        Assert.Equal(DateTimeKind.Utc, Assert.IsType<DateTime>(mug["SaleStartsUtc"]).Kind);
        Assert.Equal("cat-1, cat-2", mug["Categories"]);
        Assert.Equal("txcd_1", mug["TaxCategoryCode"]);
        Assert.Equal(2L, mug["VariantCount"]);
        Assert.Equal(false, mug["IsBundle"]);
        Assert.Equal(true, mug["Published"]);
        Assert.Equal("owner-1", mug["Owner"]);

        var kit = rows[2];
        Assert.Equal(true, kit["IsBundle"]);
        Assert.Equal("EUR", kit["Currency"]);
        Assert.Null(kit["Categories"]);
        Assert.Null(kit["TaxCategoryCode"]);

        var gold = rows[1];
        Assert.Equal("gold-y", gold["ContentItemVersionId"]);
        Assert.Equal("Gold (draft)", gold["Title"]);
        Assert.Equal(false, gold["Published"]);
        Assert.Equal("Service", gold["ProductType"]);
        Assert.Equal(2L, gold["PriceCount"]);
    }

    [Fact]
    public async Task Query_Products_ReadsTheDraftOfATypeThePrincipalMayEdit()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, "ViewContent:" + ProductContentType, "EditContent:" + ProductContentType);
        var query = Query(CommerceReportDataSets.ProductSource.Products);
        query.Conditions.Add(JoinKeys(CommerceReportDataSets.ContentItemIdField, "mug"));

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken));

        // Assert
        var mug = Assert.Single(rows);
        Assert.Equal("mug-c", mug["ContentItemVersionId"]);
        Assert.Equal("Mug (draft)", mug["Title"]);
        Assert.Equal(false, mug["Published"]);
    }

    [Fact]
    public async Task Query_Products_PushDownNeverDropsAMatchingRow()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);
        var keys = Query(CommerceReportDataSets.ProductSource.Products, CommerceReportDataSets.ContentItemIdField);
        keys.Conditions.Add(JoinKeys(CommerceReportDataSets.ContentItemIdField, "mug", "gold", "hidden"));
        var types = Query(CommerceReportDataSets.ProductSource.Products, CommerceReportDataSets.ContentItemIdField);
        types.Conditions.Add(Condition(ProductContentReportDataSet<ContentItem>.ContentTypeField, DataFilterOperator.Equals, "plan"));
        var none = Query(CommerceReportDataSets.ProductSource.Products, CommerceReportDataSets.ContentItemIdField);
        none.Conditions.Add(JoinKeys(CommerceReportDataSets.ContentItemIdField));

        // Act
        var keyRows = Rows(await ReportDesignerTestServices.ReadAsync(source, keys, TestContext.Current.CancellationToken));
        var typeRows = Rows(await ReportDesignerTestServices.ReadAsync(source, types, TestContext.Current.CancellationToken));
        var noRows = Rows(await ReportDesignerTestServices.ReadAsync(source, none, TestContext.Current.CancellationToken));

        // Assert
        // The hidden product is never read, even when a join asks for it.
        Assert.Equal(["gold", "mug"], keyRows.Select(row => row[CommerceReportDataSets.ContentItemIdField]));
        Assert.Equal(["silver", "gold"], typeRows.Select(row => row[CommerceReportDataSets.ContentItemIdField]));
        Assert.Empty(noRows);
    }

    [Fact]
    public async Task Aggregate_Products_IsLeftToTheReport()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);
        var query = new DataAggregateQuery
        {
            DataSet = CommerceReportDataSets.ProductSource.Products,
            Groups = [new DataAggregateGroup { Field = "ContentType" }],
            Measures = [new DataAggregateMeasure { Function = DataAggregate.Count }],
            Context = new DataSourceContext { User = _user },
        };

        // Act
        var groups = await ((IDataAggregateSource)source).AggregateAsync(query, TestContext.Current.CancellationToken);

        // Assert
        // Which products a principal reads depends on its permissions, which a statement over the whole index can't apply.
        Assert.Null(groups);
    }

    [Fact]
    public async Task Query_ProductVariants_FallBackToTheProductsSkuAndPrice()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);
        var query = Query(CommerceReportDataSets.ProductSource.Variants);
        query.Conditions.Add(JoinKeys(ProductEntryReportDataSet<ProductVariant>.ProductIdField, "mug"));

        // Act
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, query, TestContext.Current.CancellationToken));
        var all = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ProductSource.Variants), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["v-s", "v-m"], rows.Select(row => row[CommerceReportDataSets.VariantIdField]));
        Assert.Equal(rows.Count, all.Count);
        Assert.All(rows, row => Assert.Equal("mug", row[ProductEntryReportDataSet<ProductVariant>.ProductIdField]));
        Assert.All(rows, row => Assert.Equal("USD", row["Currency"]));
        Assert.All(rows, row => Assert.Equal("Mug", row["ProductTitle"]));

        var small = rows[0];
        Assert.Equal(1L, small["Position"]);
        Assert.Equal("Red / S", small["VariantTitle"]);
        Assert.Equal("MUG-S", small["Sku"]);
        Assert.Equal(18m, small["Price"]);
        Assert.Equal(true, small["HasOwnPrice"]);
        Assert.Null(small["SalePrice"]);
        Assert.Equal(true, small["Enabled"]);

        var medium = rows[1];
        Assert.Equal("MUG-1", medium["Sku"]);
        Assert.Equal(20m, medium["Price"]);
        Assert.Equal(false, medium["HasOwnPrice"]);
        Assert.Equal(15m, medium["SalePrice"]);
        Assert.Equal(25m, medium["CompareAtPrice"]);
        Assert.Equal(false, medium["Enabled"]);
    }

    [Fact]
    public async Task Query_ProductPrices_AndBundleItems_ComeFromTheirProducts()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);
        var gold = Query(CommerceReportDataSets.ProductSource.Prices);
        gold.Conditions.Add(JoinKeys(ProductEntryReportDataSet<ProductPrice>.ProductIdField, "gold"));
        var bundles = Query(CommerceReportDataSets.ProductSource.BundleItems);
        bundles.Conditions.Add(JoinKeys(ProductBundleItemsReportDataSet.BundleIdField, "kit"));

        // Act
        var all = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ProductSource.Prices), TestContext.Current.CancellationToken));
        var prices = Rows(await ReportDesignerTestServices.ReadAsync(source, gold, TestContext.Current.CancellationToken));
        var items = Rows(await ReportDesignerTestServices.ReadAsync(source, bundles, TestContext.Current.CancellationToken));

        // Assert
        // The latest silver plan, then the latest gold one; the published gold version is not read a second time.
        Assert.Equal(["silver", "silver", "gold", "gold"], all.Select(row => row[ProductEntryReportDataSet<ProductPrice>.ProductIdField]));
        Assert.Equal(["monthly", "annual"], prices.Select(row => row["PriceId"]));
        Assert.Equal([1L, 2L], prices.Select(row => row["Position"]));
        Assert.All(prices, row => Assert.Equal("gold", row[ProductEntryReportDataSet<ProductPrice>.ProductIdField]));
        Assert.Equal("Monthly", prices[0]["PriceName"]);
        Assert.Equal("Recurring", prices[0]["Kind"]);
        Assert.Equal("Month", prices[0]["Interval"]);
        Assert.Equal(1L, prices[0]["BillingDuration"]);
        Assert.Equal(10m, prices[0]["Amount"]);
        Assert.Equal("USD", prices[0]["Currency"]);
        Assert.Equal(true, prices[0]["IsDefault"]);
        Assert.Equal(14L, prices[0]["TrialDays"]);
        Assert.Equal(100m, prices[1]["Amount"]);
        Assert.Equal("Year", prices[1]["Interval"]);

        Assert.Equal(["mug", "cap"], items.Select(row => row[ProductEntryReportDataSet<ProductBundleItem>.ProductIdField]));
        Assert.All(items, row => Assert.Equal("kit", row[ProductBundleItemsReportDataSet.BundleIdField]));
        Assert.Equal(2L, items[0]["Quantity"]);
        Assert.Equal("Mug", items[0]["ProductTitle"]);
        Assert.Equal("Starter kit", items[0]["BundleTitle"]);
    }

    [Fact]
    public async Task Query_ProductAttributes_ReadsTheSearchRecordsOfReadableTypesOnly()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var source = Source(session, _viewer);
        var mug = Query(CommerceReportDataSets.ProductSource.Attributes);
        mug.Conditions.Add(JoinKeys(ProductAttributesReportDataSet.ProductIdField, "mug"));

        // Act
        var all = Rows(await ReportDesignerTestServices.ReadAsync(source, Query(CommerceReportDataSets.ProductSource.Attributes), TestContext.Current.CancellationToken));
        var rows = Rows(await ReportDesignerTestServices.ReadAsync(source, mug, TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(2, all.Count);
        Assert.Equal(["Make", "Capacity"], rows.Select(row => row["AttributeKey"]));
        Assert.All(rows, row => Assert.Equal("mug", row[ProductAttributesReportDataSet.ProductIdField]));
        Assert.Equal("acme", rows[0]["Value"]);
        Assert.Equal("Acme", rows[0]["Label"]);
        Assert.Equal(0.3m, rows[1]["NumberValue"]);
        Assert.DoesNotContain(all, row => row.ContainsValue("secret"));
    }

    [Fact]
    public async Task DataSourcesStartups_RegisterTheDataSourceAndTheFeatureDataSets()
    {
        // Arrange
        await using var session = _store.CreateSession();
        var services = new ServiceCollection();

        services.AddSingleton(session);
        services.AddSingleton(Authorization(_viewer));
        services.AddSingleton(Definitions());
        services.AddSingleton(new ProductContentTypes());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));

        // Act
        new DataSourcesStartup().ConfigureServices(services);
        new BundlesDataSourcesStartup().ConfigureServices(services);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var source = scope.ServiceProvider.GetRequiredService<ProductReportDataSource>();
        var dataSets = await source.GetDataSetsAsync(new DataSourceContext { User = _user }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CommerceReportDataSets.ProductSource.Name, ProductReportDataSource.Name);
        Assert.Equal(
            [
                CommerceReportDataSets.ProductSource.Products,
                CommerceReportDataSets.ProductSource.Variants,
                CommerceReportDataSets.ProductSource.Prices,
                CommerceReportDataSets.ProductSource.BundleItems,
            ],
            dataSets.Select(dataSet => dataSet.Name));
    }

    private static ContentItem Mug(string version, bool published, bool latest, string title, string sku)
    {
        return Item("mug", version, ProductContentType, title, published, latest, day: 0, item =>
        {
            item.Alter<ProductPart>(part =>
            {
                part.Sku = sku;
                part.Price = 20m;
                part.CompareAtPrice = 25m;
                part.SalePrice = 15m;
                part.SaleStartsUtc = _start;
                part.SaleEndsUtc = _start.AddDays(10);
                part.Featured = true;
            });
            item.Alter<TaxationPart>(part => part.TaxCategoryCode = "txcd_1");
            item.Alter<ProductVariantsPart>(part =>
            {
                part.Options = [new ProductOption { Name = "Color", Values = ["Red"] }, new ProductOption { Name = "Size", Values = ["S", "M"] }];
                part.Variants =
                [
                    new ProductVariant { VariantId = "v-s", Sku = "MUG-S", Price = 18m, Options = new(StringComparer.OrdinalIgnoreCase) { ["Color"] = "Red", ["Size"] = "S" } },
                    new ProductVariant { VariantId = "v-m", Enabled = false, Options = new(StringComparer.OrdinalIgnoreCase) { ["Color"] = "Red", ["Size"] = "M" } },
                ];
            });

            ((JsonObject)item.Content)[ProductContentType] = new JsonObject
            {
                ["Category"] = new JsonObject
                {
                    ["TaxonomyContentItemId"] = "taxonomy-1",
                    ["TermContentItemIds"] = new JsonArray("cat-1", "cat-2"),
                },
            };
        });
    }

    private static ContentItem Kit()
    {
        return Item("kit", "kit-a", ProductContentType, "Starter kit", published: true, latest: true, day: 3, item =>
        {
            item.Alter<ProductPart>(part =>
            {
                part.Price = 30m;
                part.Currency = "eur";
            });
            item.Alter<ProductBundlePart>(part => part.Items =
            [
                new ProductBundleItem { ContentItemId = "mug", Title = "Mug", Quantity = 2 },
                new ProductBundleItem { ContentItemId = "cap", Title = "Cap", Quantity = 1 },
            ]);
        });
    }

    private static ContentItem Plan(string id, string version, bool published, bool latest, string title)
    {
        return Item(id, version, PlanContentType, title, published, latest, day: 5, item =>
        {
            item.Alter<ProductPart>(part => part.Price = null);
            item.Alter<ProductPricePart>(part => part.Prices =
            [
                new ProductPrice { PriceId = "monthly", Name = "Monthly", Currency = "USD", Amount = 10m, Kind = PriceKind.Recurring, BillingDuration = 1, Interval = BillingInterval.Month, TrialDays = 14, IsDefault = true },
                new ProductPrice { PriceId = "annual", Name = "Annual", Currency = "USD", Amount = 100m, Kind = PriceKind.Recurring, BillingDuration = 1, Interval = BillingInterval.Year },
            ]);
        });
    }

    private static ContentItem Item(string id, string version, string contentType, string title, bool published, bool latest, int day, Action<ContentItem> build = null)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = version,
            ContentType = contentType,
            DisplayText = title,
            Published = published,
            Latest = latest,
            Owner = "owner-1",
            CreatedUtc = _start.AddDays(day),
            ModifiedUtc = _start.AddDays(day),
            PublishedUtc = published ? _start.AddDays(day) : null,
        };

        build?.Invoke(item);

        return item;
    }

    private static ProductSearchDocument SearchRecord(string contentItemId, string contentType, string title, params ProductAttributeValue[] attributes)
    {
        return new ProductSearchDocument
        {
            ContentItemId = contentItemId,
            ContentItemVersionId = contentItemId + "-published",
            ContentType = contentType,
            Title = title,
            Attributes = [.. attributes],
        };
    }

    private static async Task CreateIndexTablesAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc", column => column.Nullable())
            .Column<DateTime>("PublishedUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc", column => column.Nullable())
            .Column<string>("Owner", column => column.Nullable().WithLength(255))
            .Column<string>("Author", column => column.Nullable().WithLength(255))
            .Column<string>("DisplayText", column => column.Nullable().WithLength(255)));

        await schemaBuilder.CreateMapIndexTableAsync<ProductSearchIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Title", column => column.Nullable().WithLength(255))
            .Column<string>("Sku", column => column.Nullable().WithLength(255))
            .Column<decimal?>("Price", column => column.Nullable())
            .Column<bool>("IsPurchasable")
            .Column<DateTime?>("PublishedUtc", column => column.Nullable())
            .Column<string>("Culture", column => column.Nullable().WithLength(32))
            .Column<bool>("Featured", column => column.WithDefault(false))
            .Column<int?>("Position", column => column.Nullable()));

        await schemaBuilder.CreateMapIndexTableAsync<ProductAttributeIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("AttributeKey", column => column.WithLength(255))
            .Column<string>("TextValue", column => column.Nullable().WithLength(255))
            .Column<decimal?>("NumberValue", column => column.Nullable())
            .Column<DateTime?>("DateValue", column => column.Nullable())
            .Column<string>("Label", column => column.Nullable().WithLength(255))
            .Column<int>("Level")
            .Column<int>("SortOrder")
            .Column<bool>("Searchable"));

        await schemaBuilder.CreateMapIndexTableAsync<ProductSaleIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<DateTime?>("StartsUtc", column => column.Nullable())
            .Column<DateTime?>("EndsUtc", column => column.Nullable()));
    }

    private static ProductReportDataSource Source(ISession session, params string[] grants)
    {
        var authorizationService = Authorization(grants);
        var access = new ProductReportAccess(new ProductContentTypes(), Definitions(), authorizationService);

        return new ProductReportDataSource(
        [
            new ProductsReportDataSet(session, authorizationService, access, new PassThroughStringLocalizer<ProductsReportDataSet>()),
            new ProductVariantsReportDataSet(session, authorizationService, access, new PassThroughStringLocalizer<ProductVariantsReportDataSet>()),
            new ProductPricesReportDataSet(session, authorizationService, access, new PassThroughStringLocalizer<ProductPricesReportDataSet>()),
            new ProductBundleItemsReportDataSet(session, authorizationService, access, new PassThroughStringLocalizer<ProductBundleItemsReportDataSet>()),
            new ProductAttributesReportDataSet(session, authorizationService, access, new PassThroughStringLocalizer<ProductAttributesReportDataSet>()),
        ]);
    }

    private static IContentDefinitionManager Definitions()
    {
        ContentTypeDefinition[] definitions =
        [
            ProductDefinition(ProductContentType, ProductType.Good),
            ProductDefinition(PlanContentType, ProductType.Service),
            ProductDefinition(SecretContentType, ProductType.Good),
            new ContentTypeDefinition(ArticleContentType, ArticleContentType, [], new JsonObject()),
        ];

        var manager = new Mock<IContentDefinitionManager>();

        manager.Setup(m => m.ListTypeDefinitionsAsync()).ReturnsAsync(definitions);
        manager
            .Setup(m => m.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => definitions.FirstOrDefault(definition => definition.Name == name));

        return manager.Object;
    }

    private static ContentTypeDefinition ProductDefinition(string name, ProductType kind)
    {
        var settings = new JsonObject
        {
            ["ProductPartSettings"] = new JsonObject
            {
                ["Type"] = (int)kind,
                ["DefaultCurrency"] = "usd",
            },
        };

        return new ContentTypeDefinition(
            name,
            name,
            [new ContentTypePartDefinition(nameof(ProductPart), new ContentPartDefinition(nameof(ProductPart)), settings)],
            new JsonObject());
    }

    // Grants a permission for a content type, named "{Permission}:{ContentType}", the way the content screens check it.
    private static IAuthorizationService Authorization(params string[] grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => grants.Contains($"{requirement.Permission.Name}:{(resource as ContentItem)?.ContentType}"))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private static DataCondition Condition(string field, DataFilterOperator filterOperator, params object[] values)
    {
        return new DataCondition
        {
            Field = field,
            Operator = filterOperator,
            Values = values,
        };
    }

    private static DataCondition JoinKeys(string field, params object[] keys)
    {
        return new DataCondition
        {
            Field = field,
            Operator = DataFilterOperator.In,
            Values = keys,
            IsJoinKey = true,
        };
    }

    private static DataSourceQuery Query(string dataSet, params string[] fields)
    {
        return new DataSourceQuery
        {
            DataSet = dataSet,
            Fields = new HashSet<string>(fields, StringComparer.Ordinal),
            MaxRows = 100,
            Context = new DataSourceContext { User = _user },
        };
    }

    private static List<Dictionary<string, object>> Rows(DataRowSet table)
    {
        return table.Rows
            .Select(row => table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal))
            .ToList();
    }
}
