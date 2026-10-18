using CrestApps.OrchardCore.Reviews.Models;
using CrestApps.OrchardCore.Reviews.Services;
using CrestApps.OrchardCore.Reviews.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.Reviews.Controllers;

/// <summary>
/// Commerce > Reviews: the reviews waiting for approval first, and the store's decision on each.
/// </summary>
[Admin("reviews/{action}/{itemId?}", "Reviews{action}")]
public sealed class ReviewsAdminController : Controller
{
    private const string OptionsStatus = "Options.Status";

    private readonly ReviewService _reviews;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ReviewsAdminController(
        ReviewService reviews,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ReviewsAdminController> htmlLocalizer,
        IStringLocalizer<ReviewsAdminController> stringLocalizer)
    {
        _reviews = reviews;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("reviews", "ReviewsIndex")]
    public async Task<IActionResult> Index(
        ReviewsIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReviewsPermissions.ManageReviews))
        {
            return Forbid();
        }

        options ??= new ReviewsIndexOptions();

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        ReviewStatus? status = options.Status switch
        {
            ReviewStatusFilter.Approved => ReviewStatus.Approved,
            ReviewStatusFilter.Rejected => ReviewStatus.Rejected,
            ReviewStatusFilter.All => null,
            _ => ReviewStatus.Pending,
        };

        var (count, reviews) = await _reviews.PageAsync(status, productId: null, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);

        options.Statuses =
        [
            new SelectListItem(S["Waiting for approval"], nameof(ReviewStatusFilter.Pending), options.Status == ReviewStatusFilter.Pending),
            new SelectListItem(S["Approved"], nameof(ReviewStatusFilter.Approved), options.Status == ReviewStatusFilter.Approved),
            new SelectListItem(S["Rejected"], nameof(ReviewStatusFilter.Rejected), options.Status == ReviewStatusFilter.Rejected),
            new SelectListItem(S["Any status"], nameof(ReviewStatusFilter.All), options.Status == ReviewStatusFilter.All),
        ];

        var routeData = new RouteData();

        if (options.Status != ReviewStatusFilter.Pending)
        {
            routeData.Values.TryAdd(OptionsStatus, options.Status);
        }

        var model = new ReviewsIndexViewModel
        {
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, count, routeData),
        };

        model.List = new AdminListContext<ProductReview>
        {
            Source = reviews,
            Pager = (IShape)model.Pager,
            ToolbarActions = await shapeFactory.CreateAsync("ReviewsAdminListFilters", Arguments.From(new { Options = options })),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("reviews", "ReviewsIndex")]
    public async Task<IActionResult> IndexFilterPost(ReviewsIndexOptions options)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReviewsPermissions.ManageReviews))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();

        if (options.Status != ReviewStatusFilter.Pending)
        {
            routeValues.TryAdd(OptionsStatus, options.Status);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    [HttpPost]
    public Task<IActionResult> Approve(string itemId, string returnUrl)
        => ModerateAsync(itemId, ReviewStatus.Approved, returnUrl);

    [HttpPost]
    public Task<IActionResult> Reject(string itemId, string returnUrl)
        => ModerateAsync(itemId, ReviewStatus.Rejected, returnUrl);

    [HttpPost]
    public async Task<IActionResult> Delete(string itemId, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReviewsPermissions.ManageReviews))
        {
            return Forbid();
        }

        if (await _reviews.FindAsync(itemId, HttpContext.RequestAborted) is { } review)
        {
            await _reviews.DeleteAsync(review, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(H["The review was deleted."]);
        }

        return Back(returnUrl);
    }

    private async Task<IActionResult> ModerateAsync(string itemId, ReviewStatus status, string returnUrl)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ReviewsPermissions.ManageReviews))
        {
            return Forbid();
        }

        if (await _reviews.FindAsync(itemId, HttpContext.RequestAborted) is { } review)
        {
            await _reviews.ModerateAsync(review, status, User.Identity?.Name, HttpContext.RequestAborted);
            await _notifier.SuccessAsync(status == ReviewStatus.Approved
                ? H["The review of {0} is approved and shows on the product.", review.ProductTitle]
                : H["The review of {0} was rejected.", review.ProductTitle]);
        }

        return Back(returnUrl);
    }

    private IActionResult Back(string returnUrl)
        => Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
}
