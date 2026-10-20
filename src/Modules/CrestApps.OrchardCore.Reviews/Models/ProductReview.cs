namespace CrestApps.OrchardCore.Reviews.Models;

/// <summary>
/// What a buyer thought of a product: a rating from 1 to 5 stars, and optionally a title and a few words.
/// </summary>
public sealed class ProductReview
{
    public long Id { get; set; }

    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the product's content item id.
    /// </summary>
    public string ProductId { get; set; }

    public string ProductTitle { get; set; }

    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name shown with the review.
    /// </summary>
    public string AuthorName { get; set; }

    public int Rating { get; set; }

    public string Title { get; set; }

    public string Body { get; set; }

    public ReviewStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reviewer bought the product on a paid order.
    /// </summary>
    public bool VerifiedPurchase { get; set; }

    public string OrderId { get; set; }

    /// <summary>
    /// Gets or sets the media paths of the photos the buyer added, at most three.
    /// </summary>
    public List<string> Photos { get; set; } = [];

    public DateTime CreatedUtc { get; set; }

    public DateTime ModifiedUtc { get; set; }

    public DateTime? ModeratedUtc { get; set; }

    public string ModeratedBy { get; set; }
}

public enum ReviewStatus
{
    /// <summary>
    /// Waiting for the store to approve it.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Shown on the product, and counted in its rating.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Not shown.
    /// </summary>
    Rejected = 2,
}

/// <summary>
/// A product's approved reviews, summed up.
/// </summary>
public sealed record ProductRating(int Count, decimal Average)
{
    public static readonly ProductRating None = new(0, 0m);
}
