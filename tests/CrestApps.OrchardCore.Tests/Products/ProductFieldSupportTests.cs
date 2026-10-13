using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.Services.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// Dates, times, links and long text take part in listings: dates filter by range and read as dates in the
/// address, times sort by time of day, and long text is matched by keyword search.
/// </summary>
public sealed class ProductFieldSupportTests
{
    [Fact]
    public void ADate_IsANumberThatSortsAndRangesLikeOne()
    {
        var number = ProductDateNumbers.ToNumber(new DateTime(2026, 1, 15, 18, 30, 0, DateTimeKind.Utc));

        Assert.Equal(20260115m, number);
        Assert.Equal(new DateOnly(2026, 1, 15), ProductDateNumbers.ToDate(number));
        Assert.Equal("2026-01-15", ProductDateNumbers.ToIso(number));
        Assert.Equal(number, ProductDateNumbers.Parse("2026-01-15"));
        Assert.True(ProductDateNumbers.ToNumber(new DateTime(2025, 12, 31)) < number);
    }

    [Theory]
    [InlineData(20260230)]
    [InlineData(20261301)]
    [InlineData(2026)]
    [InlineData(20260115.5)]
    public void ANumberThatIsNotADate_HasNoDate(decimal number)
        => Assert.Null(ProductDateNumbers.ToDate(number));

    [Fact]
    public void DateRanges_OfferOneRangePerYear_NewestFirst()
    {
        var buckets = ProductDateNumbers.GetYearBuckets(20230405m, 20251102m);

        Assert.Equal([(20250101m, 20251231m), (20240101m, 20241231m), (20230101m, 20231231m)], buckets.Select(bucket => (bucket.Min.Value, bucket.Max.Value)));
        Assert.True(ProductDateNumbers.IsWholeYear(20240101m, 20241231m, out var year));
        Assert.Equal(2024, year);
        Assert.Empty(ProductDateNumbers.GetYearBuckets(20250101m, 20251231m));
    }

    [Fact]
    public void ADateRange_IsWrittenAsDatesInTheAddress()
    {
        var facet = new ProductListingFacet { Key = "available", AttributeKey = "Available", Kind = ProductFacetKind.Range };
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["available_min"] = "2026-03-01",
            ["available_max"] = "2026-03-31",
        });

        var state = ProductListingState.Parse(query, [facet], [], [24], ["available"]);

        Assert.Equal((20260301m, 20260331m), (state.Ranges["available"].Min.Value, state.Ranges["available"].Max.Value));
        Assert.Equal("?available_min=2026-03-01&available_max=2026-03-31", state.ToQueryString());
    }

    [Fact]
    public void ANumberRange_IsNotReadAsDates()
    {
        var facet = new ProductListingFacet { Key = "year", AttributeKey = "Year", Kind = ProductFacetKind.Range };
        var query = new QueryCollection(new Dictionary<string, StringValues> { ["year_min"] = "2019" });

        var state = ProductListingState.Parse(query, [facet], [], [24]);

        Assert.Equal(2019m, state.Ranges["year"].Min);
        Assert.Equal("?year_min=2019", state.ToQueryString());
    }

    [Fact]
    public void LongText_IsSearchedAsItsWords()
    {
        Assert.Equal("Soft cotton tee & more", RichTextFieldAttributeValueProvider.ToPlainText("<p>Soft <strong>cotton</strong> tee &amp; more</p>"));
        Assert.Equal("Soft cotton tee", RichTextFieldAttributeValueProvider.ToPlainText("## Soft **cotton** tee"));
        Assert.Null(RichTextFieldAttributeValueProvider.ToPlainText("  "));
    }

    [Fact]
    public async Task ATime_IsListedAsTheTimeOfDay_AndSortsByMinutes()
    {
        var provider = new TimeFieldAttributeValueProvider();
        var context = new ProductAttributeValueContext
        {
            Key = "Starts",
            Value = new JsonObject { ["Value"] = "09:30:00" },
            Settings = new ProductAttributeSettings(),
        };

        var value = Assert.Single(await provider.GetValuesAsync(context));

        Assert.Equal("09:30", value.Text);
        Assert.Equal(570m, value.Number);
    }

    [Fact]
    public async Task ALink_IsListedByItsText_AndKeepsItsAddress()
    {
        var provider = new LinkFieldAttributeValueProvider();
        var context = new ProductAttributeValueContext
        {
            Key = "Manual",
            Value = new JsonObject { ["Url"] = "https://example.com/manual.pdf", ["Text"] = "Owner's manual" },
            Settings = new ProductAttributeSettings(),
        };

        var value = Assert.Single(await provider.GetValuesAsync(context));

        Assert.Equal("Owner's manual", value.Label);
        Assert.Equal("https://example.com/manual.pdf", value.Url);
    }

    [Theory]
    [InlineData(ProductAttributeValueKind.Number, true)]
    [InlineData(ProductAttributeValueKind.Date, true)]
    [InlineData(ProductAttributeValueKind.Time, true)]
    [InlineData(ProductAttributeValueKind.Text, true)]
    [InlineData(ProductAttributeValueKind.Term, true)]
    [InlineData(ProductAttributeValueKind.Boolean, false)]
    [InlineData(ProductAttributeValueKind.RichText, false)]
    public void WhichFields_CanBeSortedBy(ProductAttributeValueKind kind, bool sortable)
        => Assert.Equal(sortable, ProductListingService.IsSortable(kind));
}
