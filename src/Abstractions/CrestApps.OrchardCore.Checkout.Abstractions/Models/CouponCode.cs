namespace CrestApps.OrchardCore.Checkout.Models;

/// <summary>
/// One of the unique codes generated for a <see cref="Coupon"/>, such as single-use codes printed on cards or sent
/// one per customer. It has its own uses and limit; everything else, from the discount to the dates, is the coupon's.
/// </summary>
public sealed class CouponCode
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the code's identifier.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the coupon it was generated for.
    /// </summary>
    public string CouponId { get; set; }

    /// <summary>
    /// Gets or sets the code the customer enters.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets how many times the code may be used, or <see langword="null"/> for no limit.
    /// </summary>
    public int? MaxRedemptions { get; set; } = 1;

    /// <summary>
    /// Gets or sets how many times it has been used.
    /// </summary>
    public int RedemptionCount { get; set; }

    /// <summary>
    /// Gets or sets whether the code can be used at all.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the code was generated.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
