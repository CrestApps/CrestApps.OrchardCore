using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Seo;

namespace CrestApps.OrchardCore.Reviews.StructuredData;

/// <summary>
/// Adds the average rating of a product's approved reviews to its schema.org data, as an <c>AggregateRating</c>.
/// </summary>
internal sealed class ProductRatingStructuredDataProvider : ContentPartStructuredDataProvider<ProductPart>
{
    private readonly ProductRatingReader _ratingReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductRatingStructuredDataProvider"/> class.
    /// </summary>
    /// <param name="ratingReader">The product rating reader.</param>
    public ProductRatingStructuredDataProvider(ProductRatingReader ratingReader)
    {
        _ratingReader = ratingReader;
    }

    /// <inheritdoc/>
    public override int Order => StructuredDataProviderOrder.Parts + 10;

    /// <inheritdoc/>
    protected override async Task BuildAsync(StructuredDataContext context, ProductPart part)
    {
        var rating = await _ratingReader.GetRatingAsync(context.ContentItem.ContentItemId);

        if (rating.Count == 0)
        {
            return;
        }

        context.MainEntity["aggregateRating"] = new JsonObject
        {
            [StructuredDataConstants.Keywords.Type] = "AggregateRating",
            ["ratingValue"] = rating.Average.ToString("0.0", CultureInfo.InvariantCulture),
            ["reviewCount"] = rating.Count,
            ["bestRating"] = ReviewsConstants.MaxRating.ToString(CultureInfo.InvariantCulture),
            ["worstRating"] = "1",
        };
    }
}
