using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
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
        var discounts = new List<DiscountLine>();

        foreach (var coupon in await ResolveAsync(context?.Flow, cancellationToken))
        {
            if (Discount(context.Invoice, coupon) is { } discount)
            {
                discounts.Add(discount);
            }
        }

        return discounts;
    }

    private static DiscountLine Discount(CheckoutInvoice invoice, Coupon coupon)
    {
        if (coupon.Kind == CouponKind.BuyXGetY)
        {
            return BuyXGetY(invoice, coupon);
        }

        // The coupon reduces whichever part of the invoice it targets, so a first-cycle offer never touches
        // a setup fee and a one-time coupon never halves a recurring charge. Shipping is not part of it, so a
        // percentage is taken off the items and a minimum counts only what is bought: the items the coupon is for,
        // when it is limited to some products or categories. A free-shipping coupon counts the items the same way,
        // and takes off the shipping.
        var countedTarget = coupon.Kind == CouponKind.FreeShipping ? DiscountTarget.OneTime : coupon.Target;
        var baseAmount = CheckoutDiscountBase.GetAmount(invoice, countedTarget, coupon.AppliesTo);

        if (!HasMinimumQuantity(invoice, coupon, countedTarget, coupon.AppliesTo))
        {
            return null;
        }

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

        return amount <= 0m ? null : Line(coupon, amount, lineItemIds);
    }

    // What the customer buys has to reach the offer's minimums, then the cheapest items they can get come off.
    private static DiscountLine BuyXGetY(CheckoutInvoice invoice, Coupon coupon)
    {
        if (!coupon.IsWithinSpend(CheckoutDiscountBase.GetAmount(invoice, DiscountTarget.OneTime, coupon.CountsTowardBuy)) ||
            !HasMinimumQuantity(invoice, coupon, DiscountTarget.OneTime, coupon.CountsTowardBuy))
        {
            return null;
        }

        var lines = (invoice.LineItems ?? []).Where(line => CheckoutDiscountBase.IsDiscountable(line, DiscountTarget.OneTime));
        var amount = Money.Round(coupon.GetBuyXGetYDiscount(lines, out var shares), invoice.Currency);

        if (amount <= 0m)
        {
            return null;
        }

        var line = Line(coupon, amount, [.. shares.Keys]);
        line.Target = DiscountTarget.OneTime;
        line.LineShares = shares;

        return line;
    }

    private static bool HasMinimumQuantity(CheckoutInvoice invoice, Coupon coupon, DiscountTarget target, Func<CheckoutLineItem, bool> counts)
        => coupon.MinimumQuantity is not > 0 ||
            (invoice.LineItems ?? []).Where(line => CheckoutDiscountBase.IsDiscountable(line, target) && counts(line)).Sum(line => line.Quantity) >= coupon.MinimumQuantity.Value;

    private static DiscountLine Line(Coupon coupon, decimal amount, IList<string> lineItemIds)
        => new()
        {
            ProviderKey = ProviderKey,
            Code = coupon.Code,
            Description = string.IsNullOrEmpty(coupon.Description) ? coupon.Code : coupon.Description,
            Amount = amount,
            Target = coupon.EffectiveTarget,
            LineItemIds = lineItemIds,
            Class = coupon.DiscountClass,
            CombinesWith = coupon.CombinesWith,
            IsAutomatic = coupon.IsAutomatic,
        };

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

    private async Task<IReadOnlyList<Coupon>> ResolveAsync(CheckoutFlow flow, CancellationToken cancellationToken)
    {
        if (flow?.Session is null)
        {
            return [];
        }

        if (!flow.Session.TryGet<CheckoutCoupon>(out var applied))
        {
            applied = new CheckoutCoupon();
        }

        var coupons = new List<Coupon>();

        foreach (var code in applied.GetCodes())
        {
            var coupon = await _couponStore.GetByCodeAsync(code, cancellationToken);

            // An automatic discount applies by itself, below, whatever the buyer typed; a coupon that only takes its
            // generated codes doesn't take its own.
            if (coupon is { CanBeTyped: false })
            {
                continue;
            }

            // A code that has expired, been disabled, or run out between being entered and the invoice being
            // rebuilt simply stops applying. The customer sees the full price rather than an error, because the
            // invoice is rebuilt on every step and an error there would block a purchase they can still make. A
            // coupon limited to some sources, reference types or one customer applies only to a checkout that
            // matches.
            if (coupon is not null && coupon.IsRedeemable(_clock.UtcNow) && await _scopeEvaluator.IsInScopeAsync(coupon, flow, cancellationToken) &&
                !coupons.Any(entry => entry.ItemId == coupon.ItemId))
            {
                coupons.Add(coupon);
            }
        }

        // An automatic discount applies to every checkout that meets its conditions, unless the buyer has had it as
        // often as it allows, it has run out, or it was taken off when payment began. Checking what is left of it
        // here, not only when payment begins, is what keeps a used-up discount from showing on every new checkout.
        foreach (var coupon in await _couponStore.ListAutomaticAsync(cancellationToken) ?? [])
        {
            if (!coupon.IsRedeemable(_clock.UtcNow) ||
                applied.IsDeclined(coupon.Code) ||
                coupons.Any(entry => entry.ItemId == coupon.ItemId) ||
                !await _scopeEvaluator.IsInScopeAsync(coupon, flow, cancellationToken))
            {
                continue;
            }

            if (await _redemptions.GetAvailabilityAsync(coupon, flow, cancellationToken) == CouponAvailability.Available)
            {
                coupons.Add(coupon);
            }
        }

        return coupons;
    }
}
