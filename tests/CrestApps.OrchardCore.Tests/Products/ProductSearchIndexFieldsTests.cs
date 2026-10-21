using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Search;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A search server holds each product as the same fields whichever provider writes them, and the counts it returns
/// for a filter come back with the labels and order visitors see.
/// </summary>
public sealed class ProductSearchIndexFieldsTests
{
    [Fact]
    public void AttributeFieldNames_AreSafeForEveryProvider_AndKeepSimilarKeysApart()
    {
        var name = ProductSearchIndexFields.Attribute("Vehicle.Year");

        Assert.Matches("^a_vehicle_year_[0-9a-f]{8}$", name);
        Assert.NotEqual(name, ProductSearchIndexFields.Attribute("Vehicle-Year"));
        Assert.Equal(name, ProductSearchIndexFields.Attribute("Vehicle.Year"));
        Assert.True(ProductSearchIndexFields.Attribute(new string('x', 300)).Length <= 64);
    }

    [Fact]
    public void AFacetToken_CarriesTheLevelLabelAndOrder()
    {
        var token = ProductSearchIndexFields.ToFacetToken(new ProductAttributeValue { Key = "make", Text = "ford", Label = "Ford", Level = 1, Order = 3 });

        Assert.True(ProductSearchIndexFields.TryParseFacetToken(token, out var level, out var value));
        Assert.Equal(1, level);
        Assert.Equal("ford", value.Value);
        Assert.Equal("Ford", value.Label);
        Assert.Equal(3, value.Order);
        Assert.False(ProductSearchIndexFields.TryParseFacetToken("ford", out _, out _));
    }

    [Fact]
    public void FacetCounts_KeepTheRequestedLevel_AndCountARenamedValueOnce()
    {
        string Token(string text, string label, int level)
            => ProductSearchIndexFields.ToFacetToken(new ProductAttributeValue { Text = text, Label = label, Level = level });

        var result = ProductSearchIndexFields.ToFacetResult(
            new ProductFacetRequest { Key = "model", AttributeKey = "Vehicle.Make", Level = 2 },
            [
                new(Token("ford", "Ford", 1), 5),
                new(Token("focus", "Focus", 2), 3),
                new(Token("focus", "Focus (old)", 2), 1),
                new("not a token", 9),
            ]);

        var value = Assert.Single(result.Values);
        Assert.Equal("model", result.Key);
        Assert.Equal("focus", value.Value);
        Assert.Equal(4, value.Count);
    }

    [Fact]
    public void AProduct_IsWrittenAsFiltersFacetsNumbersAndSortValues()
    {
        var document = new ProductSearchDocument
        {
            Id = 42,
            ContentItemId = "car1",
            ContentType = "Vehicle",
            Title = "Ford Focus",
            Sku = "FF-1",
            Price = 18000m,
            Featured = true,
            Attributes =
            [
                new() { Key = "Vehicle.Make", Text = "ford", Label = "Ford", Level = 1 },
                new() { Key = "Vehicle.Make", Text = "focus", Label = "Focus", Level = 2 },
                new() { Key = "Vehicle.Year", Text = "2021", Number = 2021m },
                new() { Key = "Vehicle.Description", Text = new string('d', 400), Label = new string('d', 400), Searchable = true },
                new() { Key = "Vehicle.Color", Text = "blue", Label = "Blue", Searchable = true },
            ],
        };

        var fields = ProductSearchIndexFields.GetFields(document).ToDictionary(field => field.Name);

        Assert.Equal(ProductSearchIndexFields.NeutralCulture, fields[ProductSearchIndexFields.Culture].Values[0]);
        Assert.Equal(1m, fields[ProductSearchIndexFields.Featured].Values[0]);
        Assert.Equal(42m, fields[ProductSearchIndexFields.Sequence].Values[0]);
        Assert.Equal("ford focus", fields[ProductSearchIndexFields.TitleSort].Values[0]);
        Assert.Equal(["ford", "focus"], fields[ProductSearchIndexFields.Values("Vehicle.Make")].Values);
        Assert.Equal(2, fields[ProductSearchIndexFields.Facets("Vehicle.Make")].Values.Count);
        Assert.Equal("focus", fields[ProductSearchIndexFields.SortText("Vehicle.Make")].Values[0]);
        Assert.Equal(2021m, fields[ProductSearchIndexFields.SortNumber("Vehicle.Year")].Values[0]);

        // Long text is searched, but not filtered or counted on.
        Assert.False(fields.ContainsKey(ProductSearchIndexFields.Values("Vehicle.Description")));
        var keywords = (string)fields[ProductSearchIndexFields.Keywords].Values[0];
        Assert.Contains("Blue", keywords);
        Assert.Contains("FF-1", keywords);
        Assert.Contains(new string('d', 400), keywords);

        // A product without a price or position leaves those fields out, so it sorts last.
        Assert.False(fields.ContainsKey(ProductSearchIndexFields.Position));
    }

    [Fact]
    public void Definitions_DeclareSixFieldsPerAttribute_IncludingTheBuiltInOnes_AndOneKey()
    {
        var definitions = ProductSearchIndexFields.GetDefinitions(["Vehicle.Make", "Vehicle.Make", "Vehicle.Year"]);

        Assert.Single(definitions, definition => definition.IsKey);
        Assert.Equal(12 + (8 * 6), definitions.Count);
        Assert.Contains(definitions, definition => definition.Name == ProductSearchIndexFields.Values(ProductAttributeKeys.InStock));
        Assert.Contains(definitions, definition => definition.Name == ProductSearchIndexFields.SortNumber(ProductAttributeKeys.Rating));
        Assert.Contains(definitions, definition => definition.Name == ProductSearchIndexFields.Facets("Vehicle.Year") && definition.IsFacetable && definition.IsCollection);
    }
}
