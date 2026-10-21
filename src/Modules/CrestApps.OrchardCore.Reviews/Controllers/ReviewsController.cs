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

    internal readonly IHtmlLocalizer H;

    public ReviewsController(
        ReviewService reviews,
        IContentManager contentManager,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ReviewsController> htmlLocalizer)
    {
        _reviews = reviews;
        _contentManager = contentManager;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpPost("reviews/{productId}")]
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

        var review = await _reviews.SubmitAsync(new ReviewSubmission
        {
            ProductId = productId,
            ProductTitle = product.DisplayText,
            UserId = userId,
            AuthorName = string.IsNullOrWhiteSpace(model.AuthorName) ? User.Identity?.Name : model.AuthorName,
            Rating = model.Rating,
            Title = model.Title,
            Body = model.Body,
        }, HttpContext.RequestAborted);

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
