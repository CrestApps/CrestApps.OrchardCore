using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// The coupon editor.
/// </summary>
public sealed class CouponEditViewModel
{
    /// <summary>
    /// Gets or sets the coupon identifier, when editing an existing one.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the code the customer enters.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the description shown on the customer's invoice.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how the reduction is calculated.
    /// </summary>
    public CouponKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the percentage taken off.
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Gets or sets the fixed amount taken off.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency a fixed amount is expressed in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what the coupon reduces.
    /// </summary>
    public DiscountTarget Target { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the coupon can be used.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets when the coupon starts being valid.
    /// </summary>
    public DateTime? StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when the coupon stops being valid.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets how many times the coupon may be redeemed in total.
    /// </summary>
    public int? MaxRedemptions { get; set; }

    /// <summary>
    /// Gets or sets the minimum amount the invoice must reach.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets how many times it has already been redeemed. It is read-only in the editor: a redemption
    /// count is a record of what happened, not a setting.
    /// </summary>
    public int RedemptionCount { get; set; }
}
