using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Checkout.ViewModels;

/// <summary>
/// Which coupons the list shows.
/// </summary>
public enum CouponStatusFilter
{
    /// <summary>
    /// Every coupon.
    /// </summary>
    All,

    /// <summary>
    /// Coupons a customer can redeem right now.
    /// </summary>
    Active,

    /// <summary>
    /// Coupons that are enabled but cannot be redeemed now: not started yet, expired, or used up.
    /// </summary>
    NotAvailable,

    /// <summary>
    /// Coupons an administrator turned off.
    /// </summary>
    Disabled,
}

/// <summary>
/// The filters of the coupons list.
/// </summary>
public class CouponsIndexOptions
{
    /// <summary>
    /// Gets or sets text matched against the code and the description.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets which coupons to show.
    /// </summary>
    public CouponStatusFilter Status { get; set; }

    /// <summary>
    /// Gets or sets the status choices.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];
}

/// <summary>
/// The coupons list.
/// </summary>
public class CouponsIndexViewModel
{
    /// <summary>
    /// Gets or sets the coupons on this page.
    /// </summary>
    public IList<Coupon> Coupons { get; set; } = [];

    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public CouponsIndexOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets whether some enabled coupon has neither a usage limit nor an end date.
    /// </summary>
    public bool HasUnboundedCoupons { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }
}
