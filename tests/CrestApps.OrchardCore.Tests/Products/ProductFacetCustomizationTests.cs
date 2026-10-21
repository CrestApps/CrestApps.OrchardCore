using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// Filter styles other modules can add, swatch colors a merchant sets, and ready-made ranges a merchant types.
/// </summary>
public sealed class ProductFacetCustomizationTests
{
    [Fact]
    public void AddStyle_RegistersAStyleForItsKinds()
    {
        var options = new ProductFacetStyleOptions();

        options.AddStyle("Stars", style => style.Kinds.Add(ProductFacetKind.Checkbox));

        var stars = options.Find("stars");

        Assert.NotNull(stars);
        Assert.Equal("Stars", stars.DisplayName.Value);
        Assert.True(stars.Supports(ProductFacetKind.Checkbox));
        Assert.False(stars.Supports(ProductFacetKind.Range));
    }

    [Fact]
    public void AddStyle_UpdatesAStyleAlreadyRegistered_AndKeepsItsPlace()
    {
        var options = new ProductFacetStyleOptions();

        options.AddStyle("List");
        options.AddStyle("Slider", style => style.Kinds.Add(ProductFacetKind.Range));
        options.AddStyle("list", style => style.Kinds.Add(ProductFacetKind.Radio));

        Assert.Equal(["List", "Slider"], options.Styles.Select(style => style.Name));
        Assert.Contains(ProductFacetKind.Radio, options.Find("List").Kinds);
    }

    [Fact]
    public void AStyleWithNoKinds_DrawsEveryKind()
    {
        var options = new ProductFacetStyleOptions();

        options.AddStyle("Compact");

        Assert.All(Enum.GetValues<ProductFacetKind>(), kind => Assert.True(options.Find("Compact").Supports(kind)));
    }

    [Theory]
    [InlineData("#191970", "#191970")]
    [InlineData("#FFF", "#fff")]
    [InlineData("Navy", "navy")]
    [InlineData("Light Blue", "lightblue")]
    [InlineData(" dark-green ", "darkgreen")]
    public void TryNormalize_AcceptsHexColorsAndColorNames(string color, string expected)
    {
        Assert.True(ProductSwatchColors.TryNormalize(color, out var css));
        Assert.Equal(expected, css);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#gggggg")]
    [InlineData("red; background: url(x)")]
    [InlineData("rgb(0,0,0)")]
    [InlineData("Very Very Long Color Name Indeed")]
    [InlineData("Honda")]
    [InlineData("skyish")]
    public void TryNormalize_RefusesAnythingElse(string color)
        => Assert.False(ProductSwatchColors.TryNormalize(color, out _));

    [Fact]
    public void Resolve_PrefersTheFieldsColor_ThenTheTermsColor_ThenTheName()
    {
        var colors = new Dictionary<string, string> { ["Midnight"] = "#191970" };

        Assert.Equal("#191970", ProductSwatchColors.Resolve("midnight", "Midnight", colors, "#000000"));
        Assert.Equal("#336699", ProductSwatchColors.Resolve("ocean", "Ocean", colors, "#336699"));
        Assert.Equal("navy", ProductSwatchColors.Resolve("navy", "Navy", colors, null));
        Assert.Null(ProductSwatchColors.Resolve("forest-mist", "Forest Mist 2", colors, null));
    }

    [Fact]
    public void Resolve_MatchesTheFieldsColorsByValueAndIgnoringCase()
    {
        var colors = new Dictionary<string, string> { ["PEARL-WHITE"] = "#f4f4f1" };

        Assert.Equal("#f4f4f1", ProductSwatchColors.Resolve("pearl-white", "Pearl White", colors, null));
    }

    [Fact]
    public void Parse_ReadsNameEqualsColorLines_AndReportsTheRest()
    {
        var colors = ProductSwatchColors.Parse("Midnight = #191970\r\n\r\nSky = light blue\nBroken\nBad = url(x)", out var invalid);

        Assert.Equal("#191970", colors["midnight"]);
        Assert.Equal("light blue", colors["Sky"]);
        Assert.Equal(["Broken", "Bad = url(x)"], invalid);
    }

    [Fact]
    public void ReadTermField_ReadsATextFieldOnAnyPartOfTheTerm()
    {
        var term = JsonNode.Parse("""
            {
              "ContentItemId": "t1",
              "TitlePart": { "Title": "Midnight" },
              "Color": { "Swatch": { "Text": "#191970" } }
            }
            """).AsObject();

        Assert.Equal("#191970", ProductSwatchColors.ReadTermField(term, "Swatch"));
        Assert.Null(ProductSwatchColors.ReadTermField(term, "Missing"));
    }

    [Fact]
    public void TryParse_ReadsUpTo_Between_AndAndUp()
    {
        Assert.True(ProductRangeBrackets.TryParse("-25, 25-75; 75.5+", out var ranges, out _));

        Assert.Equal(
            [(null, 25m), (25m, 75m), (75.5m, null)],
            ranges.Select(range => (range.Min, range.Max)).ToArray());
    }

    [Theory]
    [InlineData("10-5")]
    [InlineData("abc")]
    [InlineData("5-5")]
    [InlineData("1,000-2,000")]
    public void TryParse_RefusesWhatItCannotRead(string text)
    {
        Assert.False(ProductRangeBrackets.TryParse(text, out var ranges, out var invalid));
        Assert.Empty(ranges);
        Assert.NotNull(invalid);
    }

    [Fact]
    public void Format_WritesRangesTheWayTheyAreTyped()
    {
        var ranges = new[]
        {
            new ProductFacetRange { Max = 25 },
            new ProductFacetRange { Min = 25, Max = 75 },
            new ProductFacetRange { Min = 75 },
        };

        Assert.Equal("-25, 25-75, 75+", ProductRangeBrackets.Format(ranges));
        Assert.True(ProductRangeBrackets.TryParse(ProductRangeBrackets.Format(ranges), out var parsed, out _));
        Assert.Equal(3, parsed.Count);
    }
}
