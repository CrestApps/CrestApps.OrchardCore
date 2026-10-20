using System.Security.Claims;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Reviews.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reviews.Drivers;

/// <summary>
/// A product's rating under its price, and its reviews and the review form at the bottom of its page.
/// </summary>
internal sealed class ProductReviewsDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    private const int ShownReviews = 20;

    private readonly ReviewService _reviews;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ReviewPhotoStore _photos;

    public ProductReviewsDisplayDriver(ReviewService reviews, IHttpContextAccessor httpContextAccessor, IAuthorizationService authorizationService, ReviewPhotoStore photos)
    {
        _reviews = reviews;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _photos = photos;
    }

    public override IDisplayResult Display(ProductPart part, BuildPartDisplayContext context)
    {
        if (context.DisplayType != "Detail")
        {
            return null;
        }

        var productId = part.ContentItem.ContentItemId;

        return Combine(
            Initialize<ProductRatingViewModel>("ProductRating", async model => model.Rating = await _reviews.GetRatingAsync(productId))
                .Location("Detail", "Content:before:5"),
            Initialize<ProductReviewsViewModel>("ProductReviews", async model =>
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var user = httpContext?.User;
                var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

                model.ProductId = productId;
                model.ProductTitle = part.ContentItem.DisplayText;
                model.Rating = await _reviews.GetRatingAsync(productId);
                model.Reviews = await _reviews.ListApprovedAsync(productId, ShownReviews);
                model.IsSignedIn = !string.IsNullOrEmpty(userId);
                model.DefaultAuthorName = user?.Identity?.Name;
                model.ReturnUrl = httpContext is null ? null : httpContext.Request.PathBase + httpContext.Request.Path;
                model.CanAddPhotos = _photos.IsAvailable;

                if (model.IsSignedIn && await _authorizationService.AuthorizeAsync(user, ReviewsPermissions.SubmitReviews))
                {
                    var eligibility = await _reviews.GetEligibilityAsync(productId, userId);

                    model.CanReview = eligibility.CanReview;
                    model.Existing = eligibility.Existing;
                }
            }).Location("Detail", "Content:60"));
    }
}
