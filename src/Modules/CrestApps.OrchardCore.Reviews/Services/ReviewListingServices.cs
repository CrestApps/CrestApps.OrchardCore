using System.Globalization;
using CrestApps.OrchardCore.Products.Core.Listing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Reviews.Services;

/// <summary>
/// Puts each product's average rating and review count in its search record, so listings can filter and sort by them.
/// </summary>
internal sealed class ReviewSearchDocumentContributor : IProductSearchDocumentContributor
{
    private readonly ProductRatingReader _ratings;

    public ReviewSearchDocumentContributor(ProductRatingReader ratings)
    {
        _ratings = ratings;
    }

    public async Task ContributeAsync(ContentItem contentItem, ProductSearchDocument document)
    {
        var rating = await _ratings.GetRatingAsync(contentItem.ContentItemId);

        if (rating.Count == 0)
        {
            return;
        }

        document.Attributes.Add(new ProductAttributeValue { Key = ProductAttributeKeys.Rating, Number = rating.Average, Text = rating.Average.ToString("0.0", CultureInfo.InvariantCulture) });
        document.Attributes.Add(new ProductAttributeValue { Key = ProductAttributeKeys.ReviewCount, Number = rating.Count, Text = rating.Count.ToString(CultureInfo.InvariantCulture) });
    }
}

/// <summary>
/// Adds "Customer rating" to the automatic filters and sorts of listings.
/// </summary>
internal sealed class ReviewListingEntryProvider : IProductListingEntryProvider
{
    internal readonly IStringLocalizer S;

    public ReviewListingEntryProvider(IStringLocalizer<ReviewListingEntryProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public IEnumerable<ProductListingFacet> GetFacets()
        =>
        [
            new ProductListingFacet
            {
                Key = "avg-rating",
                AttributeKey = ProductAttributeKeys.Rating,
                Label = S["Customer rating"],
                Kind = ProductFacetKind.Range,
                ShowRanges = true,
                Ranges = [new ProductFacetRange { Min = 4 }, new ProductFacetRange { Min = 3 }, new ProductFacetRange { Min = 2 }],
            },
        ];

    public IEnumerable<ProductListingSort> GetSorts()
        =>
        [
            new ProductListingSort { Key = "avg-rating-desc", Label = S["Customer rating: highest first"], AttributeKey = ProductAttributeKeys.Rating, Descending = true },
        ];
}
