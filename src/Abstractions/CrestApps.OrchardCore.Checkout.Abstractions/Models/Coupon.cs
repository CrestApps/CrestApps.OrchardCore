using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// A code a customer enters to pay less.
/// </summary>
/// <remarks>
/// The two fields that matter most are the ones that stop a coupon costing more than it was meant to. A
/// campaign code that leaks and has no usage limit is unbounded liability, and one with no end date keeps
/// discounting long after the promotion is over. Both are enforced when the coupon is applied, not merely
/// documented on it.
/// </remarks>
public sealed class Coupon : CatalogItem
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the code the customer enters. It is matched without case sensitivity, because nobody
    /// types a promotion code the way it was printed.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the description the customer sees on their invoice.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how the reduction is calculated.
    /// </summary>
    public CouponKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the percentage taken off, when <see cref="Kind"/> is
    /// <see cref="CouponKind.Percentage"/>.
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Gets or sets the fixed amount taken off, when <see cref="Kind"/> is
    /// <see cref="CouponKind.FixedAmount"/>.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency a fixed amount is expressed in. A fixed-amount coupon only
    /// applies to an invoice in the same currency, because ten dollars off is not ten euros off.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what the coupon reduces.
    /// </summary>
    public DiscountTarget Target { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the coupon can be used at all.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the coupon starts being valid.
    /// </summary>
    public DateTime? StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the coupon stops being valid.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets how many times the coupon may be redeemed in total, or <see langword="null"/> for no
    /// limit.
    /// </summary>
    public int? MaxRedemptions { get; set; }

    /// <summary>
    /// Gets or sets how many times it has been redeemed.
    /// </summary>
    public int RedemptionCount { get; set; }

    /// <summary>
    /// Gets or sets the minimum amount the invoice must reach before the coupon applies.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the coupon was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the coupon was last changed.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Returns whether the coupon may be used right now.
    /// </summary>
    /// <param name="utcNow">The current UTC time.</param>
    public bool IsRedeemable(DateTime utcNow)
    {
        if (!IsEnabled)
        {
            return false;
        }

        if (StartsUtc.HasValue && utcNow < StartsUtc.Value)
        {
            return false;
        }

        if (EndsUtc.HasValue && utcNow > EndsUtc.Value)
        {
            return false;
        }

        return !MaxRedemptions.HasValue || RedemptionCount < MaxRedemptions.Value;
    }

    /// <summary>
    /// Returns the amount this coupon takes off a given base, before rounding.
    /// </summary>
    /// <param name="baseAmount">The amount the coupon applies to.</param>
    /// <param name="currency">The invoice currency.</param>
    public decimal GetDiscount(decimal baseAmount, string currency)
    {
        if (baseAmount <= 0m)
        {
            return 0m;
        }

        if (MinimumAmount.HasValue && baseAmount < MinimumAmount.Value)
        {
            return 0m;
        }

        if (Kind == CouponKind.Percentage)
        {
            var percentage = Math.Clamp(Percentage, 0m, 100m);

            return baseAmount * percentage / 100m;
        }

        // A fixed amount in another currency is not convertible here, and guessing a rate would charge the
        // customer something nobody chose.
        if (!string.IsNullOrEmpty(Currency) && !string.Equals(Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return 0m;
        }

        return Math.Min(Amount, baseAmount);
    }
}

/// <summary>
/// How a <see cref="Coupon"/> calculates its reduction.
/// </summary>
public enum CouponKind
{
    /// <summary>
    /// A percentage of what the coupon applies to.
    /// </summary>
    Percentage = 0,

    /// <summary>
    /// A fixed amount in a specific currency.
    /// </summary>
    FixedAmount = 1,
}
