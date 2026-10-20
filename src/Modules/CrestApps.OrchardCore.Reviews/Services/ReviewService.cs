using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Reviews.Indexes;
using CrestApps.OrchardCore.Reviews.Models;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Reviews.Services;

/// <summary>
/// Takes buyers' reviews, moderates them, and sums up each product's rating.
/// </summary>
public sealed class ReviewService
{
    private const string Collection = ReviewsConstants.CollectionName;
    private const int OrderPageSize = 100;

    private readonly ISession _session;
    private readonly IOrderStore _orderStore;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<IProductSearchIndexRefresher> _refreshers;
    private readonly IClock _clock;

    public ReviewService(
        ISession session,
        IOrderStore orderStore,
        ISiteService siteService,
        IEnumerable<IProductSearchIndexRefresher> refreshers,
        IClock clock)
    {
        _session = session;
        _orderStore = orderStore;
        _siteService = siteService;
        _refreshers = refreshers;
        _clock = clock;
    }

    /// <summary>
    /// Returns whether the user can review the product: whether the site needs them to have bought it, and the paid
    /// order they bought it on, if any.
    /// </summary>
    public async Task<ReviewEligibility> GetEligibilityAsync(string productId, string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(userId))
        {
            return new ReviewEligibility(false, null, null);
        }

        var settings = await _siteService.GetSettingsAsync<ReviewSettings>();
        var order = await FindPaidOrderAsync(productId, userId, cancellationToken);
        var existing = await FindByUserAsync(productId, userId, cancellationToken);

        return new ReviewEligibility(order is not null || !settings.BuyersOnly, order, existing);
    }

    /// <summary>
    /// Takes the user's review of the product, or replaces the one they wrote before. It waits for approval when the
    /// site asks for it.
    /// </summary>
    public async Task<ProductReview> SubmitAsync(ReviewSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var eligibility = await GetEligibilityAsync(submission.ProductId, submission.UserId, cancellationToken);

        if (!eligibility.CanReview)
        {
            return null;
        }

        var settings = await _siteService.GetSettingsAsync<ReviewSettings>();
        var now = _clock.UtcNow;
        var review = eligibility.Existing ?? new ProductReview
        {
            ItemId = IdGenerator.GenerateId(),
            ProductId = submission.ProductId,
            UserId = submission.UserId,
            CreatedUtc = now,
        };

        var wasApproved = review.Status == ReviewStatus.Approved;

        review.ProductTitle = submission.ProductTitle;
        review.AuthorName = Clip(submission.AuthorName, 80);
        review.Rating = Math.Clamp(submission.Rating, 1, ReviewsConstants.MaxRating);
        review.Title = Clip(submission.Title, ReviewsConstants.MaxTitleLength);
        review.Body = Clip(submission.Body, ReviewsConstants.MaxBodyLength);

        if (submission.Photos is not null)
        {
            review.Photos = [.. submission.Photos.Take(ReviewPhotoStore.MaxPhotos)];
        }
        review.VerifiedPurchase = eligibility.Order is not null;
        review.OrderId = eligibility.Order?.ItemId;
        review.Status = settings.RequireApproval ? ReviewStatus.Pending : ReviewStatus.Approved;
        review.ModeratedUtc = null;
        review.ModeratedBy = null;
        review.ModifiedUtc = now;

        await _session.SaveAsync(review, collection: Collection, cancellationToken: cancellationToken);

        if (wasApproved || review.Status == ReviewStatus.Approved)
        {
            await RefreshAsync(review.ProductId, cancellationToken);
        }

        return review;
    }

    public Task<ProductReview> FindAsync(string itemId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(itemId)
            ? Task.FromResult<ProductReview>(null)
            : _session.Query<ProductReview, ProductReviewIndex>(index => index.ItemId == itemId, collection: Collection).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Approves or rejects a review, and updates its product's rating.
    /// </summary>
    public async Task ModerateAsync(ProductReview review, ReviewStatus status, string moderatedBy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        if (review.Status == status)
        {
            return;
        }

        review.Status = status;
        review.ModeratedUtc = _clock.UtcNow;
        review.ModeratedBy = moderatedBy;

        await _session.SaveAsync(review, collection: Collection, cancellationToken: cancellationToken);
        await RefreshAsync(review.ProductId, cancellationToken);
    }

    public async Task DeleteAsync(ProductReview review, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(review);

        _session.Delete(review, Collection);

        if (review.Status == ReviewStatus.Approved)
        {
            await RefreshAsync(review.ProductId, cancellationToken);
        }
    }

    /// <summary>
    /// Sums up the product's approved reviews.
    /// </summary>
    public Task<ProductRating> GetRatingAsync(string productId, CancellationToken cancellationToken = default)
        => new ProductRatingReader(_session).GetRatingAsync(productId, cancellationToken);

    /// <summary>
    /// Returns the product's latest approved reviews.
    /// </summary>
    public async Task<IReadOnlyList<ProductReview>> ListApprovedAsync(string productId, int take, CancellationToken cancellationToken = default)
        => [.. await _session
            .Query<ProductReview, ProductReviewIndex>(index => index.ProductId == productId && index.Status == ReviewStatus.Approved, collection: Collection)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(take)
            .ListAsync(cancellationToken)];

    /// <summary>
    /// Returns a page of reviews for moderation, newest first.
    /// </summary>
    public async Task<(int Count, IReadOnlyList<ProductReview> Reviews)> PageAsync(ReviewStatus? status, string productId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _session.Query<ProductReview, ProductReviewIndex>(collection: Collection);

        if (status.HasValue)
        {
            var value = status.Value;

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrEmpty(productId))
        {
            query = query.Where(index => index.ProductId == productId);
        }

        var count = await query.CountAsync(cancellationToken);
        var reviews = await query.OrderByDescending(index => index.CreatedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. reviews]);
    }

    public Task<int> CountPendingAsync(CancellationToken cancellationToken = default)
        => _session.QueryIndex<ProductReviewIndex>(index => index.Status == ReviewStatus.Pending, collection: Collection).CountAsync(cancellationToken);

    private Task<ProductReview> FindByUserAsync(string productId, string userId, CancellationToken cancellationToken)
        => _session
            .Query<ProductReview, ProductReviewIndex>(index => index.ProductId == productId && index.UserId == userId, collection: Collection)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<Order> FindPaidOrderAsync(string productId, string userId, CancellationToken cancellationToken)
    {
        var query = new OrderQuery { UserId = userId, Statuses = [OrderStatus.Paid] };

        for (var skip = 0; ; skip += OrderPageSize)
        {
            var (_, orders) = await _orderStore.PageAsync(query, skip, OrderPageSize, cancellationToken);

            if (orders.FirstOrDefault(order => order.Lines?.Any(line => line.ContentItemId == productId) == true) is { } order)
            {
                return order;
            }

            if (orders.Count < OrderPageSize)
            {
                return null;
            }
        }
    }

    // The product's rating is in its search record, for listings to filter and sort by.
    private async Task RefreshAsync(string productId, CancellationToken cancellationToken)
    {
        foreach (var refresher in _refreshers)
        {
            await refresher.RefreshAsync([productId], cancellationToken);
        }
    }

    private static string Clip(string value, int length)
    {
        var trimmed = value?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length <= length ? trimmed : trimmed[..length];
    }
}

/// <summary>
/// Whether a user can review a product, the paid order they bought it on, and the review they wrote before.
/// </summary>
public sealed record ReviewEligibility(bool CanReview, Order Order, ProductReview Existing);

public sealed class ReviewSubmission
{
    public string ProductId { get; set; }

    public string ProductTitle { get; set; }

    public string UserId { get; set; }

    public string AuthorName { get; set; }

    public int Rating { get; set; }

    public string Title { get; set; }

    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the review's photos, as media paths, or <see langword="null"/> to keep the ones it has.
    /// </summary>
    public IReadOnlyList<string> Photos { get; set; }
}
