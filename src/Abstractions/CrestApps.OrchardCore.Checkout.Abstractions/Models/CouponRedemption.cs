namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// One use of a coupon: held while a checkout pays, then redeemed when it completes, or given back.
/// </summary>
/// <remarks>
/// A use is held when payment starts, like stock, so two buyers racing for a coupon's last use can't both pay for
/// it: the second is told before paying. The hold lasts as long as the checkout can, and the checkout lets it go
/// when it is canceled, fails or expires.
/// </remarks>
public sealed class CouponRedemption
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the redemption.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the code that was used, as the coupon spells it.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the checkout session that used it.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets what the checkout paid for, such as <c>Order</c>.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of what the checkout paid for, such as the order id.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets the customer who used it: their user id, or <see langword="null"/> for a guest.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the email the customer checked out with.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets where the use stands.
    /// </summary>
    public CouponRedemptionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the coupon's <see cref="Coupon.RedemptionCount"/> includes this use.
    /// A use recorded while the coupon couldn't be locked is counted by the next one that can.
    /// </summary>
    public bool IsCounted { get; set; }

    /// <summary>
    /// Gets or sets what the coupon took off.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="Amount"/>.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets when the use was first held or recorded.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a hold lapses if the checkout neither completes nor ends.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets when the use was redeemed.
    /// </summary>
    public DateTime? RedeemedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a hold was released or a redeemed use given back.
    /// </summary>
    public DateTime? EndedUtc { get; set; }
}

/// <summary>
/// Where a <see cref="CouponRedemption"/> stands.
/// </summary>
public enum CouponRedemptionStatus
{
    /// <summary>
    /// Held for a checkout that is paying.
    /// </summary>
    Held = 0,

    /// <summary>
    /// Used by a completed checkout.
    /// </summary>
    Redeemed = 1,

    /// <summary>
    /// Let go, because the checkout was canceled, failed or expired.
    /// </summary>
    Released = 2,

    /// <summary>
    /// Given back after it was redeemed, such as when the order was canceled.
    /// </summary>
    Returned = 3,
}

/// <summary>
/// Whether a coupon can be used by a checkout right now.
/// </summary>
public enum CouponAvailability
{
    /// <summary>
    /// It can be used.
    /// </summary>
    Available = 0,

    /// <summary>
    /// Every use is taken, or held by checkouts that are paying.
    /// </summary>
    UsedUp = 1,

    /// <summary>
    /// The customer has used it as many times as it allows one customer.
    /// </summary>
    CustomerLimitReached = 2,

    /// <summary>
    /// It is disabled, deleted, or outside its dates.
    /// </summary>
    Unavailable = 3,

    /// <summary>
    /// It couldn't be checked just now, because another checkout was using it. Trying again works.
    /// </summary>
    Busy = 4,
}
