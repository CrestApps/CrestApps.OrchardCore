using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Handlers;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class ProductListingStateTests
{
    private static readonly ProductListingFacet[] _facets =
    [
        new() { Key = "make", AttributeKey = "Model", Kind = ProductFacetKind.Checkbox },
        new() { Key = "condition", AttributeKey = "Condition", Kind = ProductFacetKind.Radio },
        new() { Key = "price", AttributeKey = ProductAttributeKeys.Price, Kind = ProductFacetKind.Range },
        new() { Key = "online", AttributeKey = ProductAttributeKeys.Purchasable, Kind = ProductFacetKind.Toggle },
    ];

    [Fact]
    public void Parse_ReadsRepeatedAndCommaSeparatedValues()
    {
        var state = Parse(("make", new StringValues(["toyota", "honda,ford"])));

        Assert.Equal(["toyota", "honda", "ford"], state.Values["make"]);
    }

    [Fact]
    public void Parse_IgnoresWhatTheListingDoesNotDefine()
    {
        var state = Parse(("color", "red"), ("sort", "random"), ("size", "1000"), ("page", "-4"));

        Assert.Empty(state.Values);
        Assert.Null(state.Sort);
        Assert.Null(state.PageSize);
        Assert.Equal(1, state.Page);
    }

    [Fact]
    public void Parse_KeepsOneValueForARadioFacet_AndOnlyTrueForAToggle()
    {
        var state = Parse(("condition", new StringValues(["new", "used"])), ("online", "yes"));

        Assert.Equal(["new"], state.Values["condition"]);
        Assert.False(state.Values.ContainsKey("online"));
    }

    [Fact]
    public void Parse_ReadsRangesWithTheInvariantCulture_AndSwapsReversedBounds()
    {
        var state = Parse(("price_min", "20000.50"), ("price_max", "10000"));

        Assert.Equal((10000m, 20000.50m), state.Ranges["price"]);
    }

    [Fact]
    public void Parse_CapsTheNumberOfValuesPerFacet()
    {
        var values = Enumerable.Range(0, 200).Select(i => $"v{i}").ToArray();

        var state = Parse(("make", new StringValues(values)));

        Assert.Equal(ProductListingState.MaxValuesPerFacet, state.Values["make"].Count);
    }

    [Fact]
    public void ToQueryString_RoundTrips()
    {
        var state = Parse(("q", "diesel"), ("make", new StringValues(["toyota", "honda"])), ("price_max", "20000"), ("sort", "price-asc"), ("view", "list"), ("page", "3"));

        var reparsed = Parse(QueryHelpersParse(state.ToQueryString()));

        Assert.Equal("diesel", reparsed.Keyword);
        Assert.Equal(["toyota", "honda"], reparsed.Values["make"]);
        Assert.Equal((null, 20000m), reparsed.Ranges["price"]);
        Assert.Equal("price-asc", reparsed.Sort);
        Assert.Equal(ProductListingView.List, reparsed.View);
        Assert.Equal(3, reparsed.Page);
    }

    [Fact]
    public void Toggle_AddsOrRemovesAValue_AndReturnsToTheFirstPage()
    {
        var state = Parse(("make", "toyota"), ("page", "4"));

        var added = state.Toggle("make", "honda", singleValue: false);
        var removed = state.Toggle("make", "toyota", singleValue: false);

        Assert.Equal(["toyota", "honda"], added.Values["make"]);
        Assert.Equal(1, added.Page);
        Assert.False(removed.Values.ContainsKey("make"));
        Assert.Equal(["toyota"], state.Values["make"]);
    }

    [Theory]
    [InlineData("ExteriorColor", "exterior-color")]
    [InlineData("Year", "year")]
    [InlineData("Product.Price", "product-price")]
    public void ToUrlKey_MakesShortReadableKeys(string attributeKey, string expected)
        => Assert.Equal(expected, ProductListingService.ToUrlKey(attributeKey));

    [Theory]
    [InlineData("", false)]
    [InlineData("?page=2&sort=price-asc&size=24&view=list", false)]
    [InlineData("?q=diesel", true)]
    [InlineData("?make=toyota", true)]
    [InlineData("?price_max=20000&page=2", true)]
    [InlineData("?make=", false)]
    public void IsFiltered_IgnoresPagingSortingAndLayout(string queryString, bool expected)
    {
        // Filtered pages ask search engines not to index them; paging, sorting and layout keep the page indexable.
        var query = new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString));

        Assert.Equal(expected, ProductListingSeoHandler.IsFiltered(query));
    }

    private static ProductListingState Parse(params (string Key, StringValues Value)[] values)
        => ProductListingState.Parse(
            new QueryCollection(values.ToDictionary(pair => pair.Key, pair => pair.Value)),
            _facets,
            ["newest", "price-asc"],
            [12, 24, 48]);

    private static (string Key, StringValues Value)[] QueryHelpersParse(string queryString)
        => Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(queryString)
            .Select(pair => (pair.Key, pair.Value))
            .ToArray();
}
