using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// The customer's own view of what they subscribe to, and the one action they can take on it.
/// </summary>
/// <remarks>
/// A subscriber who cannot see or stop their own subscription has to email somebody, and in several
/// jurisdictions a site that makes cancelling harder than subscribing is not compliant. So cancelling is
/// self-service, and it defaults to ending at the close of the period they already paid for rather than
/// immediately, because they bought that time.
/// </remarks>
[Admin("my-subscriptions/{action}/{itemId?}", "MySubscriptions{action}")]
public sealed class MySubscriptionsController : Controller
{
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="MySubscriptionsController"/> class.
    /// </summary>
    /// <param name="subscriptionManager">The subscription manager.</param>
    /// <param name="lifecycleService">The service that owns every subscription transition.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="notifier">The notifier used to surface outcomes.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="htmlLocalizer">The html localizer.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public MySubscriptionsController(
        ISubscriptionManager subscriptionManager,
        ISubscriptionLifecycleService lifecycleService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<MySubscriptionsController> htmlLocalizer,
        IStringLocalizer<MySubscriptionsController> stringLocalizer)
    {
        _subscriptionManager = subscriptionManager;
        _lifecycleService = lifecycleService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the signed-in customer's subscriptions.
    /// </summary>
    [Admin("my-subscriptions", "MySubscriptionsIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        var ownerId = CurrentUserId();

        if (string.IsNullOrEmpty(ownerId))
        {
            return Forbid();
        }

        return View(new MySubscriptionsViewModel
        {
            Subscriptions = [.. await _subscriptionManager.GetByOwnerAsync(ownerId)],
            UtcNow = _clock.UtcNow,
        });
    }

    /// <summary>
    /// Cancels one of the customer's own subscriptions.
    /// </summary>
    /// <param name="itemId">The subscription identifier.</param>
    /// <param name="immediately">Whether to end access now rather than at the close of the paid period.</param>
    [HttpPost]
    public async Task<IActionResult> Cancel(string itemId, bool immediately)
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        var subscription = await FindOwnedAsync(itemId);

        if (subscription is null)
        {
            return NotFound();
        }

        await _lifecycleService.CancelAsync(
            subscription.ItemId,
            atPeriodEnd: !immediately,
            S["Canceled by the subscriber."].Value,
            subscription.OwnerId);

        await _notifier.SuccessAsync(immediately
            ? H["Your subscription was canceled."]
            : H["Your subscription will not renew. You keep access until the end of the period you have paid for."]);

        return RedirectToAction(nameof(Index));
    }

    // Looking the subscription up and then checking the owner is what stops one customer from cancelling
    // another's subscription by guessing an id.
    private async Task<Subscription> FindOwnedAsync(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        var subscription = await _subscriptionManager.FindByIdAsync(itemId);
        var ownerId = CurrentUserId();

        if (subscription is null || string.IsNullOrEmpty(ownerId) || !string.Equals(subscription.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return null;
        }

        return subscription;
    }

    private string CurrentUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}
