using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Handlers;

/// <summary>
/// Puts the code from a discount link the buyer opened on each checkout they start, as if they had typed it. Whether
/// it applies is then decided as for any code.
/// </summary>
public sealed class DiscountLinkCheckoutHandler : CheckoutHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DiscountLinkCheckoutHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var code = _httpContextAccessor.HttpContext?.Request.Cookies[CheckoutConstants.DiscountLinkCookieName];

        // A checkout that settles a balance takes no discount; a code longer than any code is not one.
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64 || context.Session.SettlesExistingBalance)
        {
            return Task.CompletedTask;
        }

        if (!context.Session.TryGet<CheckoutCoupon>(out var applied))
        {
            applied = new CheckoutCoupon();
        }

        if (applied.Add(code.Trim()))
        {
            context.Session.Put(applied);
        }

        return Task.CompletedTask;
    }
}
