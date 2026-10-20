using System.Security.Claims;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Marketplace.Controllers;

/// <summary>
/// A vendor's own page on the storefront account: what it has sold and earned, its payouts, and setting up the Stripe
/// account it's paid to. The page is the <c>VendorDashboard</c> shape, so a theme can override it.
/// </summary>
[Authorize]
public sealed class VendorPortalController : Controller
{
    private readonly IVendorStore _vendors;
    private readonly IVendorEarningStore _earnings;
    private readonly IVendorPayoutStore _payouts;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public VendorPortalController(
        IVendorStore vendors,
        IVendorEarningStore earnings,
        IVendorPayoutStore payouts,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IHtmlLocalizer<VendorPortalController> htmlLocalizer)
    {
        _vendors = vendors;
        _earnings = earnings;
        _payouts = payouts;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet("account/vendor", Name = "VendorPortal")]
    public async Task<IActionResult> Index()
    {
        if (await GetVendorAsync() is not { } vendor)
        {
            return NotFound();
        }

        var stripe = HttpContext.RequestServices.GetService<StripeConnectVendorService>();

        if (stripe is not null)
        {
            await stripe.RefreshAsync(vendor, HttpContext.RequestAborted);
        }

        var earnings = await _earnings.ListByVendorAsync(vendor.ItemId, 500, HttpContext.RequestAborted);
        var payouts = await _payouts.ListByVendorAsync(vendor.ItemId, 100, HttpContext.RequestAborted);

        return View(await _shapeFactory.CreateAsync<VendorDashboardViewModel>("VendorDashboard", model =>
        {
            model.Vendor = vendor;
            model.Earnings = [.. earnings];
            model.Payouts = [.. payouts];
            model.Totals = MarketplaceService.GetTotals(earnings);
            model.CanConnectStripe = stripe is not null && !vendor.PayoutsEnabled;
        }));
    }

    [HttpPost("account/vendor/payouts")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUpPayouts()
    {
        if (await GetVendorAsync() is not { } vendor || HttpContext.RequestServices.GetService<StripeConnectVendorService>() is not { } stripe)
        {
            return NotFound();
        }

        var back = Url.RouteUrl("VendorPortal", values: null, protocol: Request.Scheme);

        try
        {
            return Redirect(await stripe.GetOnboardingUrlAsync(vendor, back, back, HttpContext.RequestAborted));
        }
        catch (global::Stripe.StripeException ex)
        {
            await _notifier.ErrorAsync(H["Stripe couldn't start setting up your payouts: {0}", ex.StripeError?.Message ?? ex.Message]);

            return RedirectToRoute("VendorPortal");
        }
    }

    // The first vendor the signed-in user runs.
    private async Task<Vendor> GetVendorAsync()
        => await _vendors.ListByUserAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted) is { Count: > 0 } vendors ? vendors[0] : null;
}
