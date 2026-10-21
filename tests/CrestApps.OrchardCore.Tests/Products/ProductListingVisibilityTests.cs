using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A listing can hide its filters, its sorting, or both, and still list its products in its default order.
/// </summary>
public sealed class ProductListingVisibilityTests
{
    private static readonly ProductListingSort _newest = new() { Key = "newest", AttributeKey = ProductAttributeKeys.Published, Descending = true };
    private static readonly ProductListingSort _price = new() { Key = "price-asc", AttributeKey = ProductAttributeKeys.Price };
    private static readonly ProductListingSort _recommended = new() { Key = ProductListingService.RecommendedSortKey, AttributeKey = ProductAttributeKeys.Position };

    [Fact]
    public void NewListings_ShowFiltersAndSorting()
    {
        var part = new ProductListingPart();

        Assert.True(part.ShowFilters);
        Assert.True(part.ShowSorting);
    }

    [Fact]
    public void ListingsSavedBeforeTheSwitches_StillShowFiltersAndSorting()
    {
        // A listing stored before the switches existed has neither property; it must keep its filters and sorts.
        var part = System.Text.Json.JsonSerializer.Deserialize<ProductListingPart>("""{ "DefaultSort": "price-asc" }""");

        Assert.True(part.ShowFilters);
        Assert.True(part.ShowSorting);
    }

    [Fact]
    public async Task HiddenFilters_OfferNoFilter()
    {
        var service = CreateService();
        var part = new ProductListingPart { ShowFilters = false, AutomaticFacets = false, Facets = [new ProductListingFacet { Key = "make", AttributeKey = "Vehicle.Make" }] };

        Assert.Empty(await service.GetFacetsAsync(part, []));
    }

    [Fact]
    public void DefaultSort_IsTheChosenOfferedSort()
    {
        var part = new ProductListingPart { DefaultSort = "price-asc" };

        Assert.Same(_price, ProductListingService.GetDefaultSort(part, [_newest, _price], []));
    }

    [Fact]
    public void DefaultSort_CanBeASortTheListingDoesNotOffer_WhenSortingIsHidden()
    {
        // The merchant unticked every sort but Newest, hid sorting, and still wants the plans in their own order.
        var part = new ProductListingPart { ShowSorting = false, DefaultSort = ProductListingService.RecommendedSortKey };

        Assert.Same(_recommended, ProductListingService.GetDefaultSort(part, [_newest], [_recommended, _newest, _price]));
    }

    [Fact]
    public void DefaultSort_FallsBackToTheFirstSort_WhenTheChosenOneIsGone()
    {
        var part = new ProductListingPart { DefaultSort = "mileage-asc" };

        Assert.Same(_newest, ProductListingService.GetDefaultSort(part, [_newest, _price], []));
    }

    private static ProductListingService CreateService()
        => new(null, null, null, null, null, null, null, null, null, null, null, null, null, null, Options.Create(new ProductFacetStyleOptions()), [], null);
}
