using CrestApps.OrchardCore.Reviews.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Reviews.Indexes;

public sealed class ProductReviewIndex : MapIndex
{
    public string ItemId { get; set; }

    public string ProductId { get; set; }

    public string UserId { get; set; }

    public ReviewStatus Status { get; set; }

    public int Rating { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public sealed class ProductReviewIndexProvider : IndexProvider<ProductReview>
{
    public ProductReviewIndexProvider()
    {
        CollectionName = ReviewsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<ProductReview> context)
    {
        context.For<ProductReviewIndex>()
            .Map(review => new ProductReviewIndex
            {
                ItemId = review.ItemId,
                ProductId = review.ProductId,
                UserId = review.UserId,
                Status = review.Status,
                Rating = review.Rating,
                CreatedUtc = review.CreatedUtc,
            });
    }
}
