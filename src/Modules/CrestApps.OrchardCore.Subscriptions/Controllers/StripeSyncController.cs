using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Synchronizes the priced subscription/product catalog into Stripe. This controller lives in the
/// Subscriptions module — not the provider-agnostic Stripe module — because it reads the local catalog
/// (products and subscription plans) and pushes it to Stripe. Because a <c>[Feature]</c> attribute can
/// only assign a type to a feature declared by its own module, the controller cannot be gated on the
/// separate Stripe module that way. Instead, every action verifies at runtime that the Stripe module is
/// enabled and that the caller may manage subscription settings.
/// </summary>
[Admin("stripe-sync/{action}")]
public sealed class StripeSyncController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public StripeSyncController(
        IAuthorizationService authorizationService,
        IShellFeaturesManager shellFeaturesManager,
        INotifier notifier,
        IHtmlLocalizer<StripeSyncController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _shellFeaturesManager = shellFeaturesManager;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    public async Task<IActionResult> Prices()
    {
        if (!await _shellFeaturesManager.IsFeatureEnabledAsync(StripeConstants.Feature.ModuleId))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return Forbid();
        }

        return View();
    }

    [HttpPost]
    [ActionName(nameof(Prices))]
    public async Task<IActionResult> PricesPost()
    {
        if (!await _shellFeaturesManager.IsFeatureEnabledAsync(StripeConstants.Feature.ModuleId))
        {
            return NotFound();
        }

        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return Forbid();
        }

        await StripePriceSyncService.SyncAllPricesInBackground();

        await _notifier.SuccessAsync(H["The background process to update Stripe price items has started successfully. You can safely navigate away; the process will continue in the background."]);

        return RedirectToAction(nameof(Prices));
    }
}
