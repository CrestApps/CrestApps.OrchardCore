using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Subscriptions.Controllers;

[Admin]
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
