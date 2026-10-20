using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Controllers;

/// <summary>
/// Opens a customer's referral link: remembers who referred the visitor for their first order, and gives them their
/// own single-use discount code when the store offers one.
/// </summary>
[Feature(CustomersConstants.Features.Referrals)]
public sealed class ReferralController : Controller
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(30);

    private readonly IReferralStore _store;
    private readonly ICustomerStore _customerStore;
    private readonly ICouponStore _couponStore;
    private readonly IPaymentAttemptLimiter _limiter;
    private readonly ISiteService _siteService;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;

    public ReferralController(
        IReferralStore store,
        ICustomerStore customerStore,
        ICouponStore couponStore,
        IPaymentAttemptLimiter limiter,
        ISiteService siteService,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<ReferralController> htmlLocalizer)
    {
        _store = store;
        _customerStore = customerStore;
        _couponStore = couponStore;
        _limiter = limiter;
        _siteService = siteService;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
    }

    [HttpGet("refer/{code}")]
    public async Task<IActionResult> Index(string code)
    {
        var profile = await _store.FindByCodeAsync(code);
        var home = Url.Content("~/");

        if (profile is null)
        {
            await _notifier.WarningAsync(H["That referral link isn't valid."]);

            return Redirect(home);
        }

        var referrer = await _customerStore.FindByIdAsync(profile.CustomerId);

        if (referrer is null)
        {
            await _notifier.WarningAsync(H["That referral link isn't valid."]);

            return Redirect(home);
        }

        if (!string.IsNullOrEmpty(referrer.UserId) && string.Equals(referrer.UserId, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, StringComparison.Ordinal))
        {
            await _notifier.InformationAsync(H["This is your own referral link. Share it with friends: you're rewarded for their first order."]);

            return Redirect(home);
        }

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = string.IsNullOrEmpty(Request.PathBase) ? "/" : Request.PathBase.Value,
            MaxAge = _lifetime,
        };

        var alreadyReferred = string.Equals(Request.Cookies[ReferralService.CookieName], profile.Code, StringComparison.Ordinal);

        Response.Cookies.Append(ReferralService.CookieName, profile.Code, cookieOptions);

        var settings = await _siteService.GetSettingsAsync<ReferralSettings>();
        var name = string.IsNullOrWhiteSpace(referrer.FirstName) ? referrer.GetName() : referrer.FirstName;

        // A friend gets one code of the store's chosen coupon, once: opening the link again, or from the same place
        // again and again, gives no more.
        if (!alreadyReferred &&
            !string.IsNullOrEmpty(settings.FriendCouponId) &&
            await _couponStore.FindByIdAsync(settings.FriendCouponId) is { IsAutomatic: false } coupon &&
            coupon.IsRedeemable(_clock.UtcNow) &&
            await _limiter.AcquireAsync("referral-link", HttpContext.Connection?.RemoteIpAddress?.ToString() ?? "unknown"))
        {
            var generated = await _couponStore.GenerateCodesAsync(coupon, 1, "REF-", usesPerCode: 1, HttpContext.RequestAborted);

            if (generated.Count > 0)
            {
                Response.Cookies.Append(CheckoutConstants.DiscountLinkCookieName, generated[0].Code, cookieOptions);

                await _notifier.SuccessAsync(H["{0} sent you a discount: {1}. It's taken off at checkout.", name, string.IsNullOrEmpty(coupon.Description) ? generated[0].Code : coupon.Description]);

                return Redirect(home);
            }
        }

        await _notifier.SuccessAsync(H["Welcome! {0} sent you.", name]);

        return Redirect(home);
    }
}
