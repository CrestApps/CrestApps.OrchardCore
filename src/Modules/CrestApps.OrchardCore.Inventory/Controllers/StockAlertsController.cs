using System.Security.Claims;
using CrestApps.OrchardCore.Inventory.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Caching.Memory;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Inventory.Controllers;

/// <summary>
/// Takes a shopper's request to be emailed when a sold-out product is back.
/// </summary>
public sealed class StockAlertsController : Controller
{
    // A visitor sends this many requests an hour at most, on top of the limit per email address.
    private const int MaxPerAddressPerHour = 20;

    private readonly StockAlertService _alerts;
    private readonly INotifier _notifier;
    private readonly IMemoryCache _cache;

    internal readonly IHtmlLocalizer H;

    public StockAlertsController(StockAlertService alerts, INotifier notifier, IMemoryCache cache, IHtmlLocalizer<StockAlertsController> htmlLocalizer)
    {
        _alerts = alerts;
        _notifier = notifier;
        _cache = cache;
        H = htmlLocalizer;
    }

    [HttpPost("stock/alerts", Name = "StockAlertSubscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe(string contentItemId, string variantId, string email, string returnUrl)
    {
        var address = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = "stock-alerts:" + address;
        var tries = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

            return new int[1];
        });

        StockAlertResult result;

        if (Interlocked.Increment(ref tries[0]) > MaxPerAddressPerHour)
        {
            result = StockAlertResult.Failure(H["Too many requests from here. Try again in an hour."].Value);
        }
        else
        {
            result = await _alerts.SubscribeAsync(contentItemId, variantId, email, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted);
        }

        if (Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return Json(new { succeeded = result.Succeeded, message = result.Message });
        }

        var message = new LocalizedHtmlString(result.Message, "{0}", false, result.Message);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(message);
        }
        else
        {
            await _notifier.ErrorAsync(message);
        }

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect("~/");
    }
}
