using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Controllers;

/// <summary>
/// Opens a discount link: a link with a code in it, shared in an email or a post, that takes the code off at checkout
/// without the buyer typing it.
/// </summary>
public sealed class DiscountLinkController : Controller
{
    // Long enough to browse and come back, short enough that a code from an old email doesn't follow the buyer around.
    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(7);

    private readonly ICouponStore _couponStore;
    private readonly INotifier _notifier;
    private readonly IClock _clock;

    internal readonly IHtmlLocalizer H;

    public DiscountLinkController(
        ICouponStore couponStore,
        INotifier notifier,
        IClock clock,
        IHtmlLocalizer<DiscountLinkController> htmlLocalizer)
    {
        _couponStore = couponStore;
        _notifier = notifier;
        _clock = clock;
        H = htmlLocalizer;
    }

    /// <summary>
    /// Keeps the code for the buyer's next checkout and sends them on to the page the link names, or the home page.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="redirect">The page to go to, on this site.</param>
    [HttpGet("discount/{code}")]
    public async Task<IActionResult> Apply(string code, string redirect)
    {
        var coupon = await _couponStore.GetByCodeAsync(code);

        if (coupon is { CanBeTyped: true } && coupon.IsRedeemable(_clock.UtcNow))
        {
            Response.Cookies.Append(CheckoutConstants.DiscountLinkCookieName, coupon.Code, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Path = string.IsNullOrEmpty(Request.PathBase) ? "/" : Request.PathBase.Value,
                MaxAge = _lifetime,
            });

            await _notifier.SuccessAsync(H["The code {0} will be taken off at checkout.", coupon.Code]);
        }
        else
        {
            await _notifier.WarningAsync(H["The code {0} isn't available.", code]);
        }

        return Redirect(Destination(redirect));
    }

    // A page on this site, written with or without the site's own path in front (/products or /shop/products on a
    // site at /shop); anything else goes to the home page.
    private string Destination(string redirect)
    {
        if (string.IsNullOrEmpty(redirect) || !Url.IsLocalUrl(redirect) || !redirect.StartsWith('/'))
        {
            return Url.Content("~/");
        }

        var pathBase = Request.PathBase.Value;

        if (string.IsNullOrEmpty(pathBase) ||
            string.Equals(redirect, pathBase, StringComparison.OrdinalIgnoreCase) ||
            redirect.StartsWith(pathBase + "/", StringComparison.OrdinalIgnoreCase))
        {
            return redirect;
        }

        return pathBase + redirect;
    }
}
