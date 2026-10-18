using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Applies the coupon a customer entered, and consumes it only when the purchase actually completes.
/// </summary>
/// <remarks>
/// The split between computing a discount and redeeming it is what makes a usage limit mean something. A
/// customer who applies a single-use code and then abandons the checkout must not burn it, or a leaked code
/// could be exhausted by people who never bought anything. So the coupon is evaluated freely while the
/// customer shops, a use is held when payment begins (<see cref="Handlers.CouponHoldPaymentGate"/>), and the
/// counter only moves on a completed purchase, through <see cref="ICouponRedemptionService"/>.
/// </remarks>
public sealed class CouponDiscountProvider : ICheckoutDiscountProvider
{
    /// <summary>
    /// The stable key recorded on discounts this provider contributes.
    /// </summary>
    public const string ProviderKey = "coupon";

    private readonly ICouponStore _couponStore;
    private readonly CouponScopeEvaluator _scopeEvaluator;
    private readonly ICouponRedemptionService _redemptions;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponDiscountProvider"/> class.
    /// </summary>
    /// <param name="couponStore">The coupon catalog.</param>
    /// <param name="scopeEvaluator">The rule that limits a coupon to some sources, reference types or one customer.</param>
    /// <param name="redemptions">The coupon uses.</param>
    /// <param name="clock">The clock used to check the validity window.</param>
    public CouponDiscountProvider(
        ICouponStore couponStore,
        CouponScopeEvaluator scopeEvaluator,
        ICouponRedemptionService redemptions,
        IClock clock)
    {
        _couponStore = couponStore;
        _scopeEvaluator = scopeEvaluator;
        _redemptions = redemptions;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Key => ProviderKey;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DiscountLine>> GetDiscountsAsync(CheckoutDiscountContext context, CancellationToken cancellationToken = default)
    {
        var coupon = await ResolveAsync(context?.Flow, cancellationToken);

        if (coupon is null)
        {
            return [];
        }

        var invoice = context.Invoice;

        // The coupon reduces whichever part of the invoice it targets, so a first-cycle offer never touches
        // a setup fee and a one-time coupon never halves a recurring charge. Shipping is not part of it, so a
        // percentage is taken off the items and a minimum counts only what is bought: the items the coupon is for,
        // when it is limited to some products or categories. A free-shipping coupon counts the items the same way,
        // and takes off the shipping.
        var countedTarget = coupon.Kind == CouponKind.FreeShipping ? DiscountTarget.OneTime : coupon.Target;
        var baseAmount = CheckoutDiscountBase.GetAmount(invoice, countedTarget, coupon.AppliesTo);

        decimal amount;
        IList<string> lineItemIds = null;

        if (coupon.Kind == CouponKind.FreeShipping)
        {
            amount = coupon.IsWithinSpend(baseAmount) ? CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.Shipping) : 0m;
        }
        else
        {
            amount = coupon.GetDiscount(baseAmount, invoice.Currency);

            if (coupon.HasItemLimits)
            {
                lineItemIds = [.. (invoice.LineItems ?? [])
                    .Where(line => CheckoutDiscountBase.IsDiscountable(line, coupon.Target) && coupon.AppliesTo(line))
                    .Select(line => line.ItemId)];
            }
        }

        if (amount <= 0m)
        {
            return [];
        }

        return
        [
            new DiscountLine
            {
                ProviderKey = ProviderKey,
                Code = coupon.Code,
                Description = string.IsNullOrEmpty(coupon.Description) ? coupon.Code : coupon.Description,
                Amount = amount,
                Target = coupon.EffectiveTarget,
                LineItemIds = lineItemIds,
            },
        ];
    }

    /// <inheritdoc/>
    public async Task RedeemAsync(CheckoutDiscountRedemptionContext context, CancellationToken cancellationToken = default)
    {
        foreach (var discount in context?.Discounts ?? [])
        {
            if (!string.Equals(discount.ProviderKey, ProviderKey, StringComparison.Ordinal) || string.IsNullOrEmpty(discount.Code))
            {
                continue;
            }

            await _redemptions.RedeemAsync(context.Flow, discount, cancellationToken);
        }
    }

    private async Task<Coupon> ResolveAsync(CheckoutFlow flow, CancellationToken cancellationToken)
    {
        if (flow?.Session is null || !flow.Session.TryGet<CheckoutCoupon>(out var applied) || string.IsNullOrEmpty(applied.Code))
        {
            return null;
        }

        var coupon = await _couponStore.GetByCodeAsync(applied.Code, cancellationToken);

        // A code that has expired, been disabled, or run out between being entered and the invoice being
        // rebuilt simply stops applying. The customer sees the full price rather than an error, because the
        // invoice is rebuilt on every step and an error there would block a purchase they can still make. A coupon
        // limited to some sources, reference types or one customer applies only to a checkout that matches.
        if (coupon is null || !coupon.IsRedeemable(_clock.UtcNow) || !await _scopeEvaluator.IsInScopeAsync(coupon, flow, cancellationToken))
        {
            return null;
        }

        return coupon;
    }
}
