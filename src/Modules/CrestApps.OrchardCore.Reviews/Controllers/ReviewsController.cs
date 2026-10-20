using System.Security.Claims;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Reviews.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Reviews.Controllers;

/// <summary>
/// Takes a buyer's review from the product's page.
/// </summary>
public sealed class ReviewsController : Controller
{
    private readonly ReviewService _reviews;
    private readonly IContentManager _contentManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ReviewPhotoStore _photos;

    internal readonly IHtmlLocalizer H;

    public ReviewsController(
        ReviewService reviews,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ReviewPhotoStore photos,
        IHtmlLocalizer<ReviewsController> htmlLocalizer)
    {
        _reviews = reviews;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _photos = photos;
        H = htmlLocalizer;
    }

    [HttpPost("reviews/{productId}")]
    [RequestSizeLimit(ReviewPhotoStore.MaxPhotos * ReviewPhotoStore.MaxBytes + 1024 * 1024)]
    public async Task<IActionResult> Submit(string productId, ReviewInputModel model)
    {
        var returnUrl = Url.IsLocalUrl(model?.ReturnUrl) ? model.ReturnUrl : "~/";
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        if (!await _authorizationService.AuthorizeAsync(User, ReviewsPermissions.SubmitReviews))
        {
            return Forbid();
        }

        var product = await _contentManager.GetAsync(productId, VersionOptions.Published);

        if (product?.Has<ProductPart>() != true)
        {
            return NotFound();
        }

        if (model is null || model.Rating is < 1 or > ReviewsConstants.MaxRating)
        {
            await _notifier.ErrorAsync(H["Choose how many stars you give the product."]);

            return LocalRedirect(returnUrl);
        }

        // New photos replace the review's; none sent keeps them.
        var eligibility = await _reviews.GetEligibilityAsync(productId, userId, HttpContext.RequestAborted);
        IReadOnlyList<string> photos = null;

        if (eligibility.CanReview && model.Photos?.Any(photo => photo is { Length: > 0 }) == true)
        {
            var (saved, problem) = await _photos.SaveAsync(productId, eligibility.Existing?.ItemId ?? userId, model.Photos, HttpContext.RequestAborted);

            if (problem is not null)
            {
                await _notifier.ErrorAsync(problem switch
                {
                    "too-many" => H["Add at most {0} photos.", ReviewPhotoStore.MaxPhotos],
                    "too-big" => H["Each photo can be at most 5 MB."],
                    "not-an-image" => H["Photos have to be JPEG, PNG or WebP images."],
                    _ => H["Photos can't be added here."],
                });

                return LocalRedirect(returnUrl);
            }

            photos = saved;
        }

        var previousPhotos = eligibility.Existing?.Photos.ToList() ?? [];
        var review = await _reviews.SubmitAsync(new ReviewSubmission
        {
            ProductId = productId,
            ProductTitle = product.DisplayText,
            UserId = userId,
            AuthorName = string.IsNullOrWhiteSpace(model.AuthorName) ? User.Identity?.Name : model.AuthorName,
            Rating = model.Rating,
            Title = model.Title,
            Body = model.Body,
            Photos = photos,
        }, HttpContext.RequestAborted);

        if (review is null)
        {
            await _photos.DeleteAsync(photos);
        }
        else if (photos is not null)
        {
            await _photos.DeleteAsync(previousPhotos.Except(photos));
        }

        if (review is null)
        {
            await _notifier.ErrorAsync(H["Only customers who bought this product can review it."]);
        }
        else if (review.Status == ReviewStatus.Pending)
        {
            await _notifier.SuccessAsync(H["Thank you. Your review will show once the store has approved it."]);
        }
        else
        {
            await _notifier.SuccessAsync(H["Thank you for your review."]);
        }

        return LocalRedirect(returnUrl);
    }
}
