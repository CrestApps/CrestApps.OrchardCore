using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using Xunit;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// Runs the product search against a real SQLite database, so the SQL it writes is exercised, not mocked.
/// </summary>
public sealed class SqlProductSearchServiceTests : IAsyncLifetime
{
    private const string Color = "Color";
    private const string Brand = "Brand";
    private const string Rating = "Rating";
    private const string Category = "Category";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"product-search-{Guid.NewGuid():N}.db");
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ProductSearchIndexProvider()]);
        await _store.InitializeAsync(cancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schema = new SchemaBuilder(_store.Configuration, transaction);

            await schema.CreateMapIndexTableAsync<ProductSearchIndex>(table => table
                .Column<string>(nameof(ProductSearchIndex.ContentItemId), column => column.WithLength(26))
                .Column<string>(nameof(ProductSearchIndex.ContentType), column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
                .Column<string>(nameof(ProductSearchIndex.Culture), column => column.Nullable().WithLength(32))
                .Column<string>(nameof(ProductSearchIndex.Title), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
                .Column<string>(nameof(ProductSearchIndex.Sku), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
                .Column<decimal?>(nameof(ProductSearchIndex.Price), column => column.Nullable())
                .Column<bool>(nameof(ProductSearchIndex.IsPurchasable))
                .Column<bool>(nameof(ProductSearchIndex.Featured), column => column.WithDefault(false))
                .Column<int?>(nameof(ProductSearchIndex.Position), column => column.Nullable())
                .Column<DateTime?>(nameof(ProductSearchIndex.PublishedUtc), column => column.Nullable()));

            await schema.CreateMapIndexTableAsync<ProductAttributeIndex>(table => table
                .Column<string>(nameof(ProductAttributeIndex.ContentItemId), column => column.WithLength(26))
                .Column<string>(nameof(ProductAttributeIndex.ContentType), column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
                .Column<string>(nameof(ProductAttributeIndex.AttributeKey), column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
                .Column<string>(nameof(ProductAttributeIndex.TextValue), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
                .Column<decimal?>(nameof(ProductAttributeIndex.NumberValue), column => column.Nullable())
                .Column<DateTime?>(nameof(ProductAttributeIndex.DateValue), column => column.Nullable())
                .Column<string>(nameof(ProductAttributeIndex.Label), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
                .Column<int>(nameof(ProductAttributeIndex.Level))
                .Column<int>(nameof(ProductAttributeIndex.SortOrder))
                .Column<bool>(nameof(ProductAttributeIndex.Searchable)));

            await transaction.CommitAsync(cancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            //               id      type        title                 price  color    brand       rating  category path
            await SaveAsync(session, "p1", "Apparel", "Crew Tee", 18m, "Black", "Northwind", 4.6m, ("apparel", "Apparel"), ("t-shirts", "T-Shirts"));
            await SaveAsync(session, "p2", "Apparel", "Pocket Tee", 22m, "Navy", "Contoso", 4.1m, ("apparel", "Apparel"), ("t-shirts", "T-Shirts"));
            await SaveAsync(session, "p3", "Apparel", "Zip Hoodie", 62m, "Navy", "Northwind", 4.4m, ("apparel", "Apparel"), ("hoodies", "Hoodies"));
            await SaveAsync(session, "p4", "Footwear", "Court Sneaker", 75m, "White", "Contoso", 4.6m, ("footwear", "Footwear"), ("sneakers", "Sneakers"));
            await SaveAsync(session, "p5", "Footwear", "Chelsea Boot", 160m, "Tan", "Fabrikam", 4.7m, ("footwear", "Footwear"), ("boots", "Boots"));
            await SaveAsync(session, "p6", "Footwear", "Work Boot", 145m, "Black", "Tailspin", 4.3m, ("footwear", "Footwear"), ("boots", "Boots"));

            await session.SaveChangesAsync(cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ValuesOfOneFilter_CombineWithOr_AndFiltersCombineWithAnd()
    {
        var result = await SearchAsync(query =>
        {
            query.Filters.Add(Filter("color", Color, "Black", "Navy"));
            query.Filters.Add(Filter("brand", Brand, "Northwind"));
        });

        Assert.Equal(["p1", "p3"], result.ContentItemIds.Order());
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task FacetCounts_IgnoreTheirOwnSelection_ButApplyTheOthers()
    {
        // Black is picked and the brand is Northwind. The color counts still offer every color Northwind has,
        // so a visitor can add Navy; the brand counts only count black products.
        var result = await SearchAsync(query =>
        {
            query.Filters.Add(Filter("color", Color, "Black"));
            query.Filters.Add(Filter("brand", Brand, "Northwind"));
            query.Facets.Add(new ProductFacetRequest { Key = "color", AttributeKey = Color });
            query.Facets.Add(new ProductFacetRequest { Key = "brand", AttributeKey = Brand });
        });

        Assert.Equal(new Dictionary<string, int> { ["Black"] = 1, ["Navy"] = 1 }, Counts(result, "color"));
        Assert.Equal(new Dictionary<string, int> { ["Northwind"] = 1, ["Tailspin"] = 1 }, Counts(result, "brand"));
    }

    [Fact]
    public async Task RangeFilters_IncludeTheirEdges()
    {
        var result = await SearchAsync(query => query.Filters.Add(new ProductSearchFilter
        {
            FacetKey = "price",
            AttributeKey = ProductAttributeKeys.Price,
            Min = 22m,
            Max = 75m,
        }));

        Assert.Equal(["p2", "p3", "p4"], result.ContentItemIds.Order());
    }

    [Fact]
    public async Task RangeFacets_ReportTheMinimumAndMaximum()
    {
        var result = await SearchAsync(query =>
        {
            query.Filters.Add(Filter("color", Color, "Black"));
            query.Facets.Add(new ProductFacetRequest { Key = "rating", AttributeKey = Rating, IsRange = true });
        });

        Assert.Equal(4.3m, result.Facets["rating"].Min);
        Assert.Equal(4.6m, result.Facets["rating"].Max);
    }

    [Fact]
    public async Task LevelFacets_CountOneLevelOfTheTree()
    {
        var result = await SearchAsync(query =>
        {
            query.Filters.Add(Filter("category", Category, "footwear"));
            query.Facets.Add(new ProductFacetRequest { Key = "category", AttributeKey = Category, Level = 1 });
            query.Facets.Add(new ProductFacetRequest { Key = "type", AttributeKey = Category, Level = 2 });
        });

        Assert.Equal(["p4", "p5", "p6"], result.ContentItemIds.Order());
        Assert.Equal(new Dictionary<string, int> { ["apparel"] = 3, ["footwear"] = 3 }, Counts(result, "category"));
        Assert.Equal(new Dictionary<string, int> { ["boots"] = 2, ["sneakers"] = 1 }, Counts(result, "type"));
    }

    [Theory]
    [InlineData(ProductAttributeKeys.Price, false, "p1")]
    [InlineData(ProductAttributeKeys.Price, true, "p5")]
    [InlineData(ProductAttributeKeys.Title, false, "p5")]
    [InlineData(Rating, true, "p5")]
    [InlineData(Brand, false, "p4")]
    [InlineData(Brand, true, "p6")]
    public async Task Sorts_OrderByColumnsAndAttributes(string attributeKey, bool descending, string first)
    {
        var result = await SearchAsync(query => query.Sort = new ProductListingSort { Key = "sort", AttributeKey = attributeKey, Descending = descending });

        Assert.Equal(first, result.ContentItemIds[0]);
    }

    [Fact]
    public async Task Paging_ReturnsOnePage_AndTheTotal()
    {
        var result = await SearchAsync(query =>
        {
            query.Sort = new ProductListingSort { Key = "price-asc", AttributeKey = ProductAttributeKeys.Price };
            query.Skip = 2;
            query.Take = 2;
        });

        Assert.Equal(["p3", "p4"], result.ContentItemIds);
        Assert.Equal(6, result.Total);
    }

    [Fact]
    public async Task Keyword_MatchesTitlesAndSearchableAttributes_Only()
    {
        // Brand is searchable and color is not.
        var byTitle = await SearchAsync(query => query.Keyword = "boot");
        var byBrand = await SearchAsync(query => query.Keyword = "contoso");
        var byColor = await SearchAsync(query => query.Keyword = "navy");

        Assert.Equal(["p5", "p6"], byTitle.ContentItemIds.Order());
        Assert.Equal(["p2", "p4"], byBrand.ContentItemIds.Order());
        Assert.Empty(byColor.ContentItemIds);
    }

    [Fact]
    public async Task Keyword_TreatsLikeWildcardsAsText()
    {
        var result = await SearchAsync(query => query.Keyword = "%");

        Assert.Empty(result.ContentItemIds);
    }

    [Fact]
    public async Task OnlyTheRequestedContentTypes_AreSearched()
    {
        var result = await SearchAsync(query => query.ContentTypes = ["Footwear"]);

        Assert.Equal(["p4", "p5", "p6"], result.ContentItemIds.Order());
    }

    [Fact]
    public async Task FeaturedFirst_PutsFeaturedProductsFirst_ThenTheNewest()
    {
        await MerchandiseAsync(("p2", true, null), ("p5", true, null));

        var result = await SearchAsync(query => query.Sort = new ProductListingSort { Key = "featured", AttributeKey = ProductAttributeKeys.Featured, Descending = true });

        // Featured p5 and p2, newest first, then the rest newest first.
        Assert.Equal(["p5", "p2", "p6", "p4", "p3", "p1"], result.ContentItemIds);
    }

    [Fact]
    public async Task Recommended_OrdersByPosition_ThenFeatured_ThenTheNewest()
    {
        await MerchandiseAsync(("p3", false, 2), ("p1", false, 1), ("p4", true, null));

        var result = await SearchAsync(query => query.Sort = new ProductListingSort { Key = "recommended", AttributeKey = ProductAttributeKeys.Position });

        Assert.Equal(["p1", "p3", "p4", "p6", "p5", "p2"], result.ContentItemIds);
    }

    [Fact]
    public async Task WithNoSort_FeaturedProductsComeFirst()
    {
        await MerchandiseAsync(("p1", true, null));

        var result = await SearchAsync(query => query.Sort = null);

        Assert.Equal("p1", result.ContentItemIds[0]);
    }

    [Fact]
    public async Task ACulture_ListsItsOwnProducts_AndThoseNotLocalized()
    {
        await using (var session = _store.CreateSession())
        {
            await session.SaveAsync(new ProductSearchDocument { ContentItemId = "fr", ContentType = "Apparel", Culture = "fr-FR", Title = "T-shirt", IsPurchasable = true }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveAsync(new ProductSearchDocument { ContentItemId = "en", ContentType = "Apparel", Culture = "en-US", Title = "T-shirt", IsPurchasable = true }, cancellationToken: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var french = await SearchAsync(query => query.Culture = "fr-FR");
        var everything = await SearchAsync(query => query.Culture = null);

        Assert.Contains("fr", french.ContentItemIds);
        Assert.DoesNotContain("en", french.ContentItemIds);
        Assert.Contains("p1", french.ContentItemIds);
        Assert.Equal(8, everything.Total);
    }

    private async Task MerchandiseAsync(params (string Id, bool Featured, int? Position)[] products)
    {
        await using var session = _store.CreateSession();

        foreach (var (id, featured, position) in products)
        {
            var document = await session.Query<ProductSearchDocument, ProductSearchIndex>(index => index.ContentItemId == id).FirstOrDefaultAsync(TestContext.Current.CancellationToken);

            document.Featured = featured;
            document.Position = position;

            await session.SaveAsync(document, cancellationToken: TestContext.Current.CancellationToken);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<ProductSearchResult> SearchAsync(Action<ProductSearchQuery> configure)
    {
        var query = new ProductSearchQuery { ContentTypes = ["Apparel", "Footwear"], Take = 50 };
        configure(query);

        await using var session = _store.CreateSession();

        return await new SqlProductSearchService(session).SearchAsync(query, TestContext.Current.CancellationToken);
    }

    private static ProductSearchFilter Filter(string facetKey, string attributeKey, params string[] values)
        => new() { FacetKey = facetKey, AttributeKey = attributeKey, Values = values };

    private static Dictionary<string, int> Counts(ProductSearchResult result, string facetKey)
        => result.Facets[facetKey].Values.ToDictionary(value => value.Value, value => value.Count);

    private static async Task SaveAsync(
        ISession session,
        string id,
        string type,
        string title,
        decimal price,
        string color,
        string brand,
        decimal rating,
        params (string Slug, string Label)[] categoryPath)
    {
        var document = new ProductSearchDocument
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v",
            ContentType = type,
            Title = title,
            Price = price,
            IsPurchasable = true,
            PublishedUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(int.Parse(id[1..])),
            Attributes =
            [
                new ProductAttributeValue { Key = ProductAttributeKeys.Price, Number = price },
                new ProductAttributeValue { Key = Color, Text = color, Label = color },
                new ProductAttributeValue { Key = Brand, Text = brand, Label = brand, Searchable = true },
                new ProductAttributeValue { Key = Rating, Number = rating },
            ],
        };

        for (var level = 0; level < categoryPath.Length; level++)
        {
            document.Attributes.Add(new ProductAttributeValue
            {
                Key = Category,
                Text = categoryPath[level].Slug,
                Label = categoryPath[level].Label,
                Level = level + 1,
            });
        }

        await session.SaveAsync(document, cancellationToken: TestContext.Current.CancellationToken);
    }
}
