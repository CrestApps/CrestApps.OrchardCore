using System.Security.Claims;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// The signed-in customer's subscriptions on the storefront account, where they can stop one from renewing.
/// The list is the <c>AccountSubscriptions</c> shape, so a theme can override it.
/// </summary>
[Authorize]
public sealed class AccountSubscriptionsController : Controller
{
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly ISubscriptionLifecycleService _lifecycleService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public AccountSubscriptionsController(
        ISubscriptionManager subscriptionManager,
        ISubscriptionLifecycleService lifecycleService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IClock clock,
        IShapeFactory shapeFactory,
        IStringLocalizer<AccountSubscriptionsController> stringLocalizer,
        IHtmlLocalizer<AccountSubscriptionsController> htmlLocalizer)
    {
        _subscriptionManager = subscriptionManager;
        _lifecycleService = lifecycleService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _clock = clock;
        _shapeFactory = shapeFactory;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    [HttpGet("account/subscriptions", Name = "AccountSubscriptions")]
    public async Task<IActionResult> Index()
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(ownerId) || !await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;
        var subscriptions = (await _subscriptionManager.GetByOwnerAsync(ownerId))
            .OrderBy(subscription => subscription.Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired ? 1 : 0)
            .ThenByDescending(subscription => subscription.CurrentPeriodEndUtc)
            .Select(subscription => new CustomerSubscription(subscription, subscription.IsCurrent(now)))
            .ToArray();

        return View(await _shapeFactory.CreateAsync<AccountSubscriptionsViewModel>("AccountSubscriptions", model =>
        {
            model.Subscriptions = subscriptions;
        }));
    }

    [HttpPost("account/subscriptions/{itemId}/cancel", Name = "AccountSubscriptionCancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string itemId)
    {
        var ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(ownerId) || !await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            return Forbid();
        }

        // Looking it up and then checking the owner stops one customer canceling another's by guessing an id.
        var subscription = string.IsNullOrEmpty(itemId) ? null : await _subscriptionManager.FindByIdAsync(itemId);

        if (subscription is null || !string.Equals(subscription.OwnerId, ownerId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        await _lifecycleService.CancelAsync(subscription.ItemId, atPeriodEnd: true, S["Canceled by the subscriber."].Value, ownerId);

        await _notifier.SuccessAsync(H["Your subscription will not renew. You keep access until the end of the period you have paid for."]);

        return RedirectToAction(nameof(Index));
    }
}
