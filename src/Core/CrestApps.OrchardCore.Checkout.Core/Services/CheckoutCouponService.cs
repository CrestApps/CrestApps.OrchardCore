using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Throttling;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
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
    private readonly IAttemptLimiter _limiter;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IClock _clock;
    private readonly IDistributedLock _distributedLock;

    internal readonly IStringLocalizer S;

    public CheckoutCouponService(
        ICouponStore couponStore,
        CouponScopeEvaluator scopeEvaluator,
        ICouponRedemptionService redemptions,
        IAttemptLimiter limiter,
        CheckoutInvoiceBuilder invoiceBuilder,
        IClock clock,
        IDistributedLock distributedLock,
        IStringLocalizer<CheckoutCouponService> stringLocalizer)
    {
        _couponStore = couponStore;
        _scopeEvaluator = scopeEvaluator;
        _redemptions = redemptions;
        _limiter = limiter;
        _distributedLock = distributedLock;
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

        var (coupon, tooManyTries) = await FindCodeAsync(code, clientAddress);

        if (tooManyTries)
        {
            return S["Too many codes were tried. Wait a few minutes, then try again."];
        }

        if (coupon is null)
        {
            return S["That code is not recognized."];
        }

        var availability = !coupon.IsRedeemable(_clock.UtcNow)
            ? CouponAvailability.Unavailable
            : await _redemptions.GetAvailabilityAsync(coupon, flow);

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
    /// Finds the coupon a buyer typed or followed a link to, counted against the <c>coupon-code</c> attempt policy of
    /// <paramref name="clientNetwork"/>. Codes are short enough to guess, so a network that tried too many wrong ones is
    /// refused before the code is looked up. The check, the lookup and the count run under one lock per network, so
    /// parallel tries can't all pass the check before any wrong code is counted.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="clientNetwork">The client's address or <see cref="ClientNetworkKey"/>.</param>
    /// <returns>The coupon, or <see langword="null"/> when no typeable coupon has the code; and whether the network is
    /// refused.</returns>
    public async Task<(Coupon Coupon, bool TooManyTries)> FindCodeAsync(string code, string clientNetwork)
    {
        var network = ClientNetworkKey.FromAddress(clientNetwork);
        var (locker, locked) = await _distributedLock.TryAcquireAttemptLockAsync(CheckoutConstants.AttemptPolicies.CouponCode, network);

        if (!locked)
        {
            return (null, true);
        }

        await using (locker)
        {
            if (!(await _limiter.PeekAsync(CheckoutConstants.AttemptPolicies.CouponCode, network)).Allowed)
            {
                return (null, true);
            }

            var coupon = string.IsNullOrWhiteSpace(code) ? null : await _couponStore.GetByCodeAsync(code.Trim());

            // An automatic discount's code is never typed; it applies by itself when the order qualifies. A coupon that
            // only takes the codes generated for it doesn't take its own.
            if (coupon is { CanBeTyped: false })
            {
                coupon = null;
            }

            if (coupon is null)
            {
                await _limiter.RecordFailureAsync(CheckoutConstants.AttemptPolicies.CouponCode, network);
            }

            return (coupon, false);
        }
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
