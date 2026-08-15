using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

/// <summary>
/// Synchronizes the priced subscription/product catalog into Stripe. This controller belongs to the
/// Subscriptions module — not the provider-agnostic Stripe module — because it reads the local catalog
/// (products and subscription plans) and pushes it to Stripe. It is therefore scoped to the
/// <c>Subscriptions - Stripe</c> integration feature, which is the only feature that registers
/// <see cref="StripePriceSyncService"/>.
/// </summary>
[Feature(SubscriptionConstants.Features.Stripe)]
[Admin("stripe-sync/{action}")]
public sealed class StripeSyncController : Controller
{
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public StripeSyncController(
        INotifier notifier,
        IHtmlLocalizer<StripeSyncController> htmlLocalizer)
    {
        _notifier = notifier;
        H = htmlLocalizer;
    }

    public IActionResult Prices()
    {
        return View();
    }

    [HttpPost]
    [ActionName(nameof(Prices))]
    public async Task<IActionResult> PricesPost()
    {
        await StripePriceSyncService.SyncAllPricesInBackground();

        await _notifier.SuccessAsync(H["The background process to update Stripe price items has started successfully. You can safely navigate away; the process will continue in the background."]);

        return RedirectToAction(nameof(Prices));
    }
}
