using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// Refer a friend, on the signed-in customer's storefront account: their link, what friends and they get, and the
/// friends they were rewarded for.
/// </summary>
[Authorize]
[Feature(CustomersConstants.Features.Referrals)]
public sealed class ReferralAccountController : Controller
{
    private readonly CustomerAccountPages _pages;
    private readonly ReferralService _referrals;
    private readonly ICouponStore _couponStore;
    private readonly ISiteService _siteService;
    private readonly IShapeFactory _shapeFactory;

    internal readonly IStringLocalizer S;

    public ReferralAccountController(
        CustomerAccountPages pages,
        ReferralService referrals,
        ICouponStore couponStore,
        ISiteService siteService,
        IShapeFactory shapeFactory,
        IStringLocalizer<ReferralAccountController> stringLocalizer)
    {
        _pages = pages;
        _referrals = referrals;
        _couponStore = couponStore;
        _siteService = siteService;
        _shapeFactory = shapeFactory;
        S = stringLocalizer;
    }

    [HttpGet("account/refer", Name = "AccountReferrals")]
    public async Task<IActionResult> Index()
    {
        if (await _pages.GetCustomerAsync(User, HttpContext.RequestAborted) is not { } customer)
        {
            return Forbid();
        }

        var profile = await _referrals.GetOrCreateAsync(customer.ItemId, HttpContext.RequestAborted);
        var settings = await _siteService.GetSettingsAsync<ReferralSettings>();
        var currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;
        var coupon = string.IsNullOrEmpty(settings.FriendCouponId) ? null : await _couponStore.FindByIdAsync(settings.FriendCouponId);

        return View(await _shapeFactory.CreateAsync<AccountReferralsViewModel>("AccountReferrals", model =>
        {
            model.Link = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/refer/{profile.Code}";
            model.FriendGets = coupon is null ? null : string.IsNullOrEmpty(coupon.Description) ? S["a discount on their first order"].Value : coupon.Description;
            model.YouGet = settings.RewardKind switch
            {
                ReferralRewardKind.StoreCredit => S["{0} store credit", ProductPriceFormatter.Format(settings.RewardAmount, currency)].Value,
                ReferralRewardKind.Points => S["{0:N0} points", settings.RewardPoints].Value,
                _ => null,
            };
            model.Referrals = [.. (profile.Referrals ?? []).OrderByDescending(record => record.CreatedUtc)];
        }));
    }
}
