using System.Globalization;
using CrestApps.OrchardCore.Products.AzureAI.Services;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using CrestApps.OrchardCore.Products.Elasticsearch.Services;
using CrestApps.OrchardCore.Products.Lucene.Services;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Lucene.Net.Search;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// Each search server answers a listing the way the database does: the same products in the same order, and filter
/// counts that leave out the filter's own selection.
/// </summary>
public sealed class ProductSearchProviderTests
{
    private static ProductSearchQuery VehicleQuery(string keyword = null) => new()
    {
        ContentTypes = ["Vehicle"],
        Culture = "en-US",
        Keyword = keyword,
        Filters =
        [
            new() { FacetKey = "make", AttributeKey = "Vehicle.Make", Values = ["ford", "honda"] },
            new() { FacetKey = "price", AttributeKey = ProductAttributeKeys.Price, Min = 20000m, Max = 40000m },
        ],
        Sort = new ProductListingSort { Key = "price-asc", AttributeKey = ProductAttributeKeys.Price },
        Skip = 12,
        Take = 12,
    };

    [Fact]
    public void TheSort_TiesGoToFeaturedThenNewest_AndAttributesSortByNumberThenName()
    {
        Assert.Equal(
            [ProductSearchIndexFields.Featured, ProductSearchIndexFields.PublishedUtc, ProductSearchIndexFields.Sequence],
            ProductSearchIndexQuery.GetSort(null).Select(field => field.Field));

        var featuredLast = ProductSearchIndexQuery.GetSort(new ProductListingSort { AttributeKey = ProductAttributeKeys.Featured, Descending = false });
        Assert.False(featuredLast[0].Descending);

        var byYear = ProductSearchIndexQuery.GetSort(new ProductListingSort { AttributeKey = "Vehicle.Year", Descending = true });
        Assert.Equal(ProductSearchIndexFields.SortNumber("Vehicle.Year"), byYear[0].Field);
        Assert.Equal(ProductSearchIndexFields.SortText("Vehicle.Year"), byYear[1].Field);
        Assert.True(byYear[0].Descending);
    }

    [Fact]
    public void AnIndexQuery_ListsTheVisitorsCultureAndProductsThatAreNotLocalized()
    {
        var indexQuery = ProductSearchIndexQuery.Create(VehicleQuery());

        Assert.Equal(["en-US", ProductSearchIndexFields.NeutralCulture], indexQuery.Cultures);
        Assert.Single(indexQuery.FiltersExcept("make"));
        Assert.Equal(ProductSearchIndexFields.Numbers(ProductAttributeKeys.Price), indexQuery.Filters[1].Field);
    }

    [Fact]
    public void AnIndexQuery_CountsOnlyTheFacetsThatNameAKeyAndAnAttribute()
    {
        var query = VehicleQuery();
        query.Facets =
        [
            new() { Key = "make", AttributeKey = "Vehicle.Make" },
            new() { Key = "color" },
            new() { AttributeKey = "Vehicle.Year" },
            null,
        ];

        var indexQuery = ProductSearchIndexQuery.Create(query);

        Assert.Equal("make", Assert.Single(indexQuery.Facets).Key);
        Assert.True(indexQuery.HasFilterFor("make"));
        Assert.False(indexQuery.HasFilterFor("color"));
    }

    [Fact]
    public void Lucene_MatchesEveryFilter_AndAFacetLeavesOutItsOwn()
    {
        var indexQuery = ProductSearchIndexQuery.Create(VehicleQuery("ford foc"));

        var all = LuceneProductSearchService.BuildQuery(indexQuery, excludedFacetKey: null).ToString();
        var withoutMake = LuceneProductSearchService.BuildQuery(indexQuery, "make").ToString();

        Assert.Contains("ContentType:Vehicle", all);
        Assert.Contains($"Culture:{ProductSearchIndexFields.NeutralCulture}", all);
        Assert.Contains($"{ProductSearchIndexFields.Values("Vehicle.Make")}:ford", all);
        Assert.Contains($"{ProductSearchIndexFields.Numbers(ProductAttributeKeys.Price)}:[20000 TO 40000]", all);

        // Each word matches the start of a word.
        Assert.Contains($"+{ProductSearchIndexFields.Keywords}:ford*", all);
        Assert.Contains($"+{ProductSearchIndexFields.Keywords}:foc*", all);

        Assert.DoesNotContain(ProductSearchIndexFields.Values("Vehicle.Make"), withoutMake);
        Assert.Contains(ProductSearchIndexFields.Numbers(ProductAttributeKeys.Price), withoutMake);
    }

    [Fact]
    public void Lucene_SortsProductsWithoutAValueLast_InEitherDirection()
    {
        var ascending = LuceneProductSearchService.ToSortField(new(ProductSearchIndexFields.Position, Descending: false));
        var descending = LuceneProductSearchService.ToSortField(new(ProductSearchIndexFields.Position, Descending: true));
        var byName = LuceneProductSearchService.ToSortField(new(ProductSearchIndexFields.TitleSort, Descending: false));

        Assert.Equal(double.MaxValue, Convert.ToDouble(ascending.MissingValue, CultureInfo.InvariantCulture));
        Assert.Equal(double.MinValue, Convert.ToDouble(descending.MissingValue, CultureInfo.InvariantCulture));
        Assert.True(descending.IsReverse);
        Assert.Equal(SortFieldType.STRING, byName.Type);
        Assert.Same(SortField.STRING_LAST, byName.MissingValue);
    }

    [Fact]
    public void Elasticsearch_FiltersAfterCounting_AndEachFacetCountsWithoutItsOwnFilter()
    {
        var indexQuery = ProductSearchIndexQuery.Create(VehicleQuery());
        ProductFacetRequest[] facets =
        [
            new() { Key = "make", AttributeKey = "Vehicle.Make" },
            new() { Key = "price", AttributeKey = ProductAttributeKeys.Price, IsRange = true },
            new() { Key = "body", AttributeKey = "Vehicle.Body" },
        ];

        var request = ElasticsearchProductSearchService.BuildRequest("tenant_products", indexQuery, facets);

        Assert.Equal(12, request.From);
        Assert.Equal(12, request.Size);
        Assert.Equal(2, request.PostFilter.Bool.Filter.Count);

        // The make facet counts within the price filter only; a facet with no selection counts within both.
        Assert.Single(request.Aggregations["facet0"].Filter.Bool.Filter);
        Assert.Equal(2, request.Aggregations["facet2"].Filter.Bool.Filter.Count);
        Assert.True(request.Aggregations["facet1"].Aggregations.ContainsKey("min"));
        Assert.True(request.Aggregations["facet2"].Aggregations.ContainsKey("values"));
        Assert.Equal(1 + 3, request.Sort.Count);
    }

    [Fact]
    public void AzureAISearch_WritesAFilterTheWayTheDatabaseFilters()
    {
        var indexQuery = ProductSearchIndexQuery.Create(VehicleQuery());

        var filter = AzureAIProductSearchFields.GetFilter(indexQuery);
        var withoutMake = AzureAIProductSearchFields.GetFilter(indexQuery, "make");

        Assert.Contains("(search.in(ContentType, 'Vehicle', '|'))", filter);
        Assert.Contains($"search.in(Culture, 'en-US|{ProductSearchIndexFields.NeutralCulture}', '|')", filter);
        Assert.Contains($"{ProductSearchIndexFields.Values("Vehicle.Make")}/any(v: search.in(v, 'ford|honda', '|'))", filter);
        Assert.Contains($"{ProductSearchIndexFields.MaxNumber(ProductAttributeKeys.Price)} ge 20000", filter);
        Assert.Contains($"{ProductSearchIndexFields.SortNumber(ProductAttributeKeys.Price)} le 40000", filter);
        Assert.DoesNotContain(ProductSearchIndexFields.Values("Vehicle.Make"), withoutMake);
    }

    [Fact]
    public void AzureAISearch_QuotesValues_AndPicksADelimiterNoValueContains()
    {
        var indexQuery = ProductSearchIndexQuery.Create(new ProductSearchQuery
        {
            Filters = [new() { FacetKey = "brand", AttributeKey = "Brand", Values = ["O'Neill", "A|B"] }],
        });

        var filter = AzureAIProductSearchFields.GetFilter(indexQuery);

        Assert.Contains("search.in(v, 'O''Neill,A|B', ',')", filter);
    }

    [Fact]
    public void AzureAISearch_MatchesTheStartOfEachWord_AndEscapesItsSyntax()
    {
        Assert.Equal("*", AzureAIProductSearchFields.GetSearchText(null));
        Assert.Equal("ford* foc*", AzureAIProductSearchFields.GetSearchText(" ford  foc "));
        Assert.Equal("c\\+\\+*", AzureAIProductSearchFields.GetSearchText("c++"));
    }

    [Fact]
    public void AzureAISearch_SortsProductsWithoutAValueLast()
    {
        var order = AzureAIProductSearchFields.GetOrderBy(
        [
            new(ProductSearchIndexFields.Position, Descending: false),
            new(ProductSearchIndexFields.PublishedUtc, Descending: true),
        ]).ToArray();

        Assert.Equal(
            [$"{ProductSearchIndexFields.Position}_has desc", $"{ProductSearchIndexFields.Position} asc", $"{ProductSearchIndexFields.PublishedUtc} desc"],
            order);
    }

    [Fact]
    public void AzureAISearch_DeclaresEveryFieldButListsOfNumbers()
    {
        var mappings = AzureAIProductSearchFields.GetMappings(["Vehicle.Year"]).ToDictionary(map => map.AzureFieldKey);

        Assert.True(mappings[ProductSearchIndexFields.ContentItemId].IsKey);
        Assert.False(mappings.ContainsKey(ProductSearchIndexFields.Numbers("Vehicle.Year")));
        Assert.True(mappings[ProductSearchIndexFields.Facets("Vehicle.Year")].IsFacetable);
        Assert.True(mappings[ProductSearchIndexFields.Facets("Vehicle.Year")].IsCollection);
        Assert.True(mappings[AzureAIProductSearchFields.HasValue(ProductSearchIndexFields.SortNumber("Vehicle.Year"))].IsSortable);
        Assert.All(mappings.Keys, name => Assert.Matches("^[A-Za-z][A-Za-z0-9_]*$", name));
    }
}
