using System.Globalization;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Services;
using CrestApps.OrchardCore.Marketplace.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Users.Services;

namespace CrestApps.OrchardCore.Marketplace.Controllers;

/// <summary>
/// Commerce › Vendors: the marketplace's sellers, what each has earned, and paying them out.
/// </summary>
[Admin("vendors/{action}/{itemId?}", "Vendors{action}")]
public sealed class VendorsAdminController : Controller
{
    private readonly IVendorStore _vendors;
    private readonly IVendorEarningStore _earnings;
    private readonly IVendorPayoutStore _payouts;
    private readonly MarketplaceService _marketplace;
    private readonly IUserService _userService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly IShapeFactory _shapeFactory;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public VendorsAdminController(
        IVendorStore vendors,
        IVendorEarningStore earnings,
        IVendorPayoutStore payouts,
        MarketplaceService marketplace,
        IUserService userService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IShapeFactory shapeFactory,
        IClock clock,
        IHtmlLocalizer<VendorsAdminController> htmlLocalizer,
        IStringLocalizer<VendorsAdminController> stringLocalizer)
    {
        _vendors = vendors;
        _earnings = earnings;
        _payouts = payouts;
        _marketplace = marketplace;
        _userService = userService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _shapeFactory = shapeFactory;
        _clock = clock;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("vendors", "Vendors")]
    public async Task<IActionResult> Index(string q, PagerParameters pagerParameters, [FromServices] IOptions<PagerOptions> pagerOptions)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var (count, vendors) = await _vendors.PageAsync(q, pager.GetStartIndex(), pager.PageSize, HttpContext.RequestAborted);
        var routeData = new RouteData();

        if (!string.IsNullOrWhiteSpace(q))
        {
            routeData.Values.TryAdd("q", q);
        }

        return View(new VendorsIndexViewModel
        {
            Search = q,
            List = new AdminListContext<Vendor>
            {
                Source = vendors,
                Pager = (IShape)await _shapeFactory.PagerAsync(pager, count, routeData),
            },
        });
    }

    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        ViewData["IsNew"] = true;

        return View("Edit", new VendorEditViewModel());
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(VendorEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;
        var vendor = new Vendor { ItemId = global::OrchardCore.IdGenerator.GenerateId(), CreatedUtc = now };

        if (!await ApplyAsync(model, vendor))
        {
            ViewData["IsNew"] = true;

            return View("Edit", model);
        }

        await _vendors.CreateAsync(vendor);
        await _notifier.SuccessAsync(H["The vendor was added. Choose it on its products, under Vendor."]);

        return RedirectToAction(nameof(Detail), new { itemId = vendor.ItemId });
    }

    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        if (await _vendors.FindByIdAsync(itemId) is not { } vendor)
        {
            return NotFound();
        }

        return View(new VendorEditViewModel
        {
            Name = vendor.Name,
            Email = vendor.Email,
            UserNames = string.Join(Environment.NewLine, vendor.UserNames),
            CommissionPercent = vendor.CommissionPercent?.ToString(CultureInfo.InvariantCulture),
            Country = vendor.Country,
            Status = vendor.Status,
        });
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, VendorEditViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        if (await _vendors.FindByIdAsync(itemId) is not { } vendor)
        {
            return NotFound();
        }

        if (!await ApplyAsync(model, vendor))
        {
            return View(model);
        }

        await _vendors.UpdateAsync(vendor);
        await _notifier.SuccessAsync(H["The vendor was saved."]);

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        if (await _vendors.FindByIdAsync(itemId) is not { } vendor)
        {
            return NotFound();
        }

        if (HttpContext.RequestServices.GetService<StripeConnectVendorService>() is { } stripe)
        {
            await stripe.RefreshAsync(vendor, HttpContext.RequestAborted);
        }

        var earnings = await _earnings.ListByVendorAsync(vendor.ItemId, 500, HttpContext.RequestAborted);
        var model = new VendorDashboardViewModel
        {
            Vendor = vendor,
            Earnings = [.. earnings],
            Payouts = [.. await _payouts.ListByVendorAsync(vendor.ItemId, 100, HttpContext.RequestAborted)],
            Totals = MarketplaceService.GetTotals(earnings),
            CanPayOut = _marketplace.CanPayOut && vendor.PayoutsEnabled && vendor.Status == VendorStatus.Active,
        };

        // The same dashboard the vendor sees on its own page.
        ViewData["Dashboard"] = await _shapeFactory.CreateAsync<VendorDashboardViewModel>("VendorDashboard", dashboard =>
        {
            dashboard.Vendor = model.Vendor;
            dashboard.Earnings = model.Earnings;
            dashboard.Payouts = model.Payouts;
            dashboard.Totals = model.Totals;
        });

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Release(string itemId, string orderId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        var released = await _marketplace.ReleaseOrderAsync(itemId, orderId, HttpContext.RequestAborted);

        await _notifier.SuccessAsync(released > 0 ? H["What the vendor earned on the order was released."] : H["Nothing was held for that order."]);

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    [HttpPost]
    public async Task<IActionResult> PayOut(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, MarketplacePermissions.ManageMarketplace))
        {
            return Forbid();
        }

        if (await _vendors.FindByIdAsync(itemId) is not { } vendor)
        {
            return NotFound();
        }

        var (paid, failures) = await _marketplace.PayOutAsync(vendor, HttpContext.RequestAborted);

        foreach (var failure in failures)
        {
            await _notifier.ErrorAsync(H["{0}", failure]);
        }

        if (failures.Count == 0)
        {
            await _notifier.SuccessAsync(paid > 0m ? H["The vendor was paid out."] : H["Nothing was ready to pay out."]);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private async Task<bool> ApplyAsync(VendorEditViewModel model, Vendor vendor)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), S["Enter the vendor's name."]);
        }

        decimal? commission = null;

        if (!string.IsNullOrWhiteSpace(model.CommissionPercent))
        {
            if (!decimal.TryParse(model.CommissionPercent.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed is < 0m or > 100m)
            {
                ModelState.AddModelError(nameof(model.CommissionPercent), S["Enter the commission as a percent from 0 to 100, or leave it empty for the store's."]);
            }
            else
            {
                commission = parsed;
            }
        }

        var userIds = new List<string>();
        var userNames = new List<string>();

        foreach (var name in (model.UserNames ?? string.Empty).Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await _userService.GetUserAsync(name) is { } user && user is global::OrchardCore.Users.Models.User known)
            {
                userIds.Add(known.UserId);
                userNames.Add(known.UserName);
            }
            else
            {
                ModelState.AddModelError(nameof(model.UserNames), S["There's no user named {0}.", name]);
            }
        }

        if (!ModelState.IsValid)
        {
            return false;
        }

        vendor.Name = model.Name.Trim();
        vendor.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        vendor.UserIds = userIds;
        vendor.UserNames = userNames;
        vendor.CommissionPercent = commission;
        vendor.Country = CountryCodeUtility.IsCountryCode(model.Country) ? CountryCodeUtility.Normalize(model.Country) : null;
        vendor.Status = Enum.IsDefined(model.Status) ? model.Status : VendorStatus.Active;
        vendor.ModifiedUtc = _clock.UtcNow;

        return true;
    }
}
