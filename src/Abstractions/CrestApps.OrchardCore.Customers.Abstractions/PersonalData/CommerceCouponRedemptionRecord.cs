namespace CrestApps.OrchardCore.Customers.PersonalData;

/// <summary>
/// A coupon the person redeemed in a completed checkout.
/// </summary>
public sealed class CommerceCouponRedemptionRecord
{
    /// <summary>
    /// Gets or sets the checkout session the coupon was redeemed in.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// Gets or sets the coupon code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the description shown on the invoice.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency of the amount.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the amount the coupon took off.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets when the checkout completed.
    /// </summary>
    public DateTime? RedeemedUtc { get; set; }
}
