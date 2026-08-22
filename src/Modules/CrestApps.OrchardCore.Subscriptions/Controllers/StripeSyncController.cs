using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Synchronizes the priced subscription catalog into Stripe. The controller lives in the Subscriptions
/// module — not the provider-agnostic Stripe module — because it reads the local subscription catalog and
/// can therefore not be pushed down to a provider that must remain unaware of subscriptions.
/// </summary>
/// <remarks>
/// The controller does not consult <c>IShellFeaturesManager</c>. Instead it is gated on the presence of
/// <see cref="StripePriceSyncService"/>, which is registered only by the <c>StripeStartup</c> class — the
/// integration that activates when the Subscriptions and Stripe features are both enabled. When that
/// service is not registered the actions return <see cref="ControllerBase.NotFound()"/>, so the route is
/// inert unless the Stripe integration is active. This follows the "gate on service registration rather
/// than on a feature check" convention.
/// </remarks>
[Admin]
public sealed class StripeSyncController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSyncController"/> class.
    /// </summary>
    /// <param name="authorizationService">The service used to authorize the current user.</param>
    /// <param name="notifier">The notifier used to surface user feedback.</param>
    /// <param name="htmlLocalizer">The localizer for user-facing messages.</param>
    public StripeSyncController(
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<StripeSyncController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Renders the confirmation page for synchronizing subscription prices into Stripe.
    /// </summary>
    /// <returns>
    /// The confirmation view; a forbidden result when the user is not authorized; or a not-found result
    /// when the Stripe integration is not enabled.
    /// </returns>
    [HttpGet]
    [Admin("stripe-sync/prices", "StripeSyncPrices")]
    public async Task<IActionResult> Prices()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return Forbid();
        }

        if (HttpContext.RequestServices.GetService<StripePriceSyncService>() is null)
        {
            return NotFound();
        }

        return View();
    }

    /// <summary>
    /// Starts the background synchronization of subscription prices into Stripe.
    /// </summary>
    /// <returns>
    /// A redirect to <see cref="Prices"/>; a forbidden result when the user is not authorized; or a
    /// not-found result when the Stripe integration is not enabled.
    /// </returns>
    [HttpPost]
    [ActionName(nameof(Prices))]
    [Admin("stripe-sync/prices")]
    public async Task<IActionResult> PricesPost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, SubscriptionPermissions.ManageSubscriptionSettings))
        {
            return Forbid();
        }

        if (HttpContext.RequestServices.GetService<StripePriceSyncService>() is null)
        {
            return NotFound();
        }

        await StripePriceSyncService.SyncAllPricesInBackground();

        await _notifier.SuccessAsync(H["The background process to update Stripe price items has started successfully. You can safely navigate away; the process will continue in the background."]);

        return RedirectToAction(nameof(Prices));
    }
}
