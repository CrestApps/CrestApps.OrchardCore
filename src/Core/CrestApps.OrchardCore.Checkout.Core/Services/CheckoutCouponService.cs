using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Puts promotion codes on a checkout and takes them off. The Payment step's box and the commerce API both go
/// through here, so a code is checked the same way wherever it's typed.
/// </summary>
public sealed class CheckoutCouponService
{
    private readonly ICouponStore _couponStore;
    private readonly CouponScopeEvaluator _scopeEvaluator;
    private readonly ICouponRedemptionService _redemptions;
    private readonly IPaymentAttemptLimiter _limiter;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public CheckoutCouponService(
        ICouponStore couponStore,
        CouponScopeEvaluator scopeEvaluator,
        ICouponRedemptionService redemptions,
        IPaymentAttemptLimiter limiter,
        CheckoutInvoiceBuilder invoiceBuilder,
        IClock clock,
        IStringLocalizer<CheckoutCouponService> stringLocalizer)
    {
        _couponStore = couponStore;
        _scopeEvaluator = scopeEvaluator;
        _redemptions = redemptions;
        _limiter = limiter;
        _invoiceBuilder = invoiceBuilder;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns whether the checkout takes no codes: one that settles an existing balance the settling module doesn't
    /// take discounts off.
    /// </summary>
    public static bool RefusesDiscounts(CheckoutFlow flow)
        => flow?.Session is CheckoutSession { SettlesExistingBalance: true, AcceptsDiscountsOnBalance: false };

    /// <summary>
    /// Returns the codes on the checkout, in the order they were entered.
    /// </summary>
    public static IReadOnlyList<string> GetCodes(CheckoutFlow flow)
        => flow.Session.TryGet<CheckoutCoupon>(out var applied) ? applied.GetCodes() : [];

    /// <summary>
    /// Puts a code on the checkout and rebuilds the invoice. Returns why it wasn't, or <see langword="null"/> when it
    /// was. <paramref name="clientAddress"/> is who is trying codes, since short codes can be guessed.
    /// </summary>
    public async Task<string> ApplyAsync(CheckoutFlow flow, string code, string clientAddress)
    {
        ArgumentNullException.ThrowIfNull(flow);

        code = code?.Trim();

        if (string.IsNullOrEmpty(code))
        {
            return S["Enter a code."];
        }

        if (RefusesDiscounts(flow))
        {
            return S["This payment takes no promotion codes."];
        }

        // Codes are short enough to guess, so how fast they can be tried from one address is limited.
        if (!await _limiter.AcquireAsync("coupon-code", clientAddress ?? "unknown"))
        {
            return S["Too many codes were tried. Wait a few minutes, then try again."];
        }

        var coupon = await _couponStore.GetByCodeAsync(code);

        // An automatic discount's code is never typed; it applies by itself when the order qualifies. A coupon that
        // only takes the codes generated for it doesn't take its own.
        if (coupon is { CanBeTyped: false })
        {
            coupon = null;
        }

        var availability = coupon is null || !coupon.IsRedeemable(_clock.UtcNow)
            ? CouponAvailability.Unavailable
            : await _redemptions.GetAvailabilityAsync(coupon, flow);

        if (coupon is null)
        {
            return S["That code is not recognized."];
        }

        // A coupon limited to some sources, kinds of charge or one customer is refused everywhere else, here where the
        // customer can still do something about it, and again by the discount provider.
        if (!await _scopeEvaluator.IsInScopeAsync(coupon, flow))
        {
            return S["That code can't be used for this payment."];
        }

        if (availability == CouponAvailability.CustomerLimitReached)
        {
            return S["You have already used that code as many times as it allows."];
        }

        // Used up counts the uses held by checkouts that are paying right now: if one of them is let go, the code works
        // again.
        if (availability != CouponAvailability.Available)
        {
            return S["That code is no longer available."];
        }

        var applied = flow.Session.TryGet<CheckoutCoupon>(out var existing) ? existing : new CheckoutCoupon();

        if (!applied.Add(coupon.Code))
        {
            return S["A checkout takes up to {0} codes. Remove one to add another.", CheckoutCoupon.MaxCodes];
        }

        flow.Session.Put(applied);

        // The invoice is rebuilt here rather than at completion, so the customer sees what the code actually took off
        // before they agree to pay.
        await _invoiceBuilder.BuildAsync(flow);

        return null;
    }

    /// <summary>
    /// Takes a code off the checkout and rebuilds the invoice. Returns whether it was on it.
    /// </summary>
    public async Task<bool> RemoveAsync(CheckoutFlow flow, string code)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!flow.Session.TryGet<CheckoutCoupon>(out var applied) || !applied.Remove(code))
        {
            return false;
        }

        flow.Session.Put(applied);
        await _invoiceBuilder.BuildAsync(flow);

        return true;
    }
}
