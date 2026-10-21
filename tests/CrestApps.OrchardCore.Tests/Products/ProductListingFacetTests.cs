using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.Services.Attributes;
using OrchardCore.Modules.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

public sealed class ProductListingFacetTests
{
    [Fact]
    public void GetRangeBuckets_UsesRoundEdges_AndOpenEnds()
    {
        var buckets = ProductListingService.GetRangeBuckets(18m, 249m);

        Assert.Equal(
            [(null, 50m), (50m, 100m), (100m, 150m), (150m, 200m), (200m, null)],
            buckets.Select(bucket => (bucket.Min, bucket.Max)).ToArray());
    }

    [Fact]
    public void GetRangeBuckets_WorksForYears()
    {
        var buckets = ProductListingService.GetRangeBuckets(2016m, 2025m);

        Assert.Equal(
            [(null, 2018m), (2018m, 2020m), (2020m, 2022m), (2022m, 2024m), (2024m, null)],
            buckets.Select(bucket => (bucket.Min, bucket.Max)).ToArray());
    }

    [Fact]
    public void GetRangeBuckets_KeepsTheNumberOfRangesReadable()
    {
        // The nearest round step, 100,000, would make seven ranges; the next one up, 200,000, makes four.
        var buckets = ProductListingService.GetRangeBuckets(189_000m, 789_000m);

        Assert.Equal(
            [(null, 200_000m), (200_000m, 400_000m), (400_000m, 600_000m), (600_000m, null)],
            buckets.Select(bucket => (bucket.Min, bucket.Max)).ToArray());
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(10, 2)]
    public void GetRangeBuckets_IsEmpty_WhenThereIsNothingToSplit(decimal min, decimal max)
        => Assert.Empty(ProductListingService.GetRangeBuckets(min, max));

    [Theory]
    [InlineData("Pickup Trucks", "pickup-trucks")]
    [InlineData("Mercedes-Benz", "mercedes-benz")]
    [InlineData("Crème Brûlée", "creme-brulee")]
    [InlineData("  T-Shirts & Tops ", "t-shirts-tops")]
    [InlineData("3 Series", "3-series")]
    public void Slugify_MakesReadableUrlValues(string name, string expected)
        => Assert.Equal(expected, new TaxonomyTermLookup(null, new SlugService()).Slugify(name, "fallback"));

    [Theory]
    [InlineData("")]
    [InlineData("***")]
    public void Slugify_FallsBack_WhenTheNameHasNoLettersOrDigits(string name)
        => Assert.Equal("fallback", new TaxonomyTermLookup(null, new SlugService()).Slugify(name, "fallback"));
}
