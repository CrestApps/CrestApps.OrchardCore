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
/// customer shops, and the counter only moves on a completed purchase, under a lock so two checkouts
/// finishing at the same moment cannot both take the last redemption.
/// </remarks>
public sealed class CouponDiscountProvider : ICheckoutDiscountProvider
{
    /// <summary>
    /// The stable key recorded on discounts this provider contributes.
    /// </summary>
    public const string ProviderKey = "coupon";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly ICouponStore _couponStore;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponDiscountProvider"/> class.
    /// </summary>
    /// <param name="couponStore">The coupon catalog.</param>
    /// <param name="distributedLock">The lock that serializes redemptions of one coupon.</param>
    /// <param name="clock">The clock used to check the validity window.</param>
    /// <param name="logger">The logger.</param>
    public CouponDiscountProvider(
        ICouponStore couponStore,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<CouponDiscountProvider> logger)
    {
        _couponStore = couponStore;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
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
        // a setup fee and a one-time coupon never halves a recurring charge.
        var baseAmount = coupon.Target == DiscountTarget.FirstCycle
            ? invoice.FirstRecurringPaymentAmount ?? 0m
            : invoice.InitialPaymentAmount ?? 0m;

        var amount = coupon.GetDiscount(baseAmount, invoice.Currency);

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
                Target = coupon.Target,
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

            await RedeemOneAsync(discount.Code, cancellationToken);
        }
    }

    private async Task RedeemOneAsync(string code, CancellationToken cancellationToken)
    {
        // Two checkouts completing at the same instant would otherwise both read the same count and both
        // write count + 1, so the last redemption of a single-use code could be taken twice.
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync("COUPON_" + code.ToUpperInvariant(), _lockTimeout, _lockExpiration);

        if (!locked)
        {
            // Losing a redemption count is bad, but blocking a completed purchase is worse: the customer has
            // already paid. The miscount is logged so it can be reconciled.
            _logger.LogWarning("Could not acquire the lock to redeem coupon '{Code}'. Its redemption count may be short by one.", code);

            return;
        }

        await using var _ = locker;

        var coupon = await _couponStore.GetByCodeAsync(code, cancellationToken);

        if (coupon is null)
        {
            return;
        }

        coupon.RedemptionCount++;

        await _couponStore.UpdateAsync(coupon, cancellationToken);
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
        // invoice is rebuilt on every step and an error there would block a purchase they can still make.
        return coupon is not null && coupon.IsRedeemable(_clock.UtcNow) ? coupon : null;
    }
}
