using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Models;
using YesSql;

namespace CrestApps.OrchardCore.Reviews.Services;

/// <summary>
/// Sums up a product's approved reviews. It is apart from <see cref="ReviewService"/> because the product search record
/// reads it, and the review service refreshes that record: one depending on the other would go round in a circle.
/// </summary>
public sealed class ProductRatingReader
{
    private readonly ISession _session;

    public ProductRatingReader(ISession session)
    {
        _session = session;
    }

    public async Task<ProductRating> GetRatingAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(productId))
        {
            return ProductRating.None;
        }

        var ratings = (await _session
            .QueryIndex<ProductReviewIndex>(index => index.ProductId == productId && index.Status == ReviewStatus.Approved, collection: ReviewsConstants.CollectionName)
            .ListAsync(cancellationToken))
            .Select(index => index.Rating)
            .ToArray();

        return ratings.Length == 0
            ? ProductRating.None
            : new ProductRating(ratings.Length, Math.Round((decimal)ratings.Average(), 1, MidpointRounding.AwayFromZero));
    }
}
