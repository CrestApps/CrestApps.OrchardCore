using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

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
    /// Gets or sets the currencies the site sells in, or <see langword="null"/> when the site keeps no list of
    /// them, in which case the code is typed.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Currencies { get; set; }

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
    /// Gets or sets the most the items may come to for the coupon to apply.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets how many times one customer may use the coupon.
    /// </summary>
    public int? MaxRedemptionsPerCustomer { get; set; }

    public CouponMethod Method { get; set; }

    public bool OnlyGeneratedCodes { get; set; }

    [BindNever]
    public int GeneratedCodes { get; set; }

    [BindNever]
    public int UsedGeneratedCodes { get; set; }

    [BindNever]
    public string DiscountLinkUrl { get; set; }

    public int? MinimumQuantity { get; set; }

    public int? BuyQuantity { get; set; }

    public int? GetQuantity { get; set; }

    public int? MaxApplicationsPerOrder { get; set; }

    public bool CombinesWithProductDiscounts { get; set; }

    public bool CombinesWithOrderDiscounts { get; set; }

    public bool CombinesWithShippingDiscounts { get; set; }

    /// <summary>
    /// Gets or sets what other modules add to the editor, such as the products a coupon is limited to.
    /// </summary>
    [BindNever]
    public object Editor { get; set; }

    /// <summary>
    /// Gets or sets the latest uses of the coupon.
    /// </summary>
    [BindNever]
    public IReadOnlyList<CouponRedemption> Redemptions { get; set; } = [];

    /// <summary>
    /// Gets or sets how each use's reference reads, by <c>{ReferenceType}|{ReferenceId}</c>, from the
    /// <see cref="Services.ICheckoutReferenceDescriber"/>s.
    /// </summary>
    [BindNever]
    public IReadOnlyDictionary<string, Services.CheckoutReferenceDescription> References { get; set; } = new Dictionary<string, Services.CheckoutReferenceDescription>();

    /// <summary>
    /// Gets or sets how many uses checkouts hold right now.
    /// </summary>
    [BindNever]
    public int HeldCount { get; set; }

    /// <summary>
    /// Gets or sets the transaction sources the coupon is limited to, separated by commas, or empty for any source.
    /// </summary>
    public string Sources { get; set; }

    /// <summary>
    /// Gets or sets the reference types the coupon is limited to, separated by commas, or empty for any.
    /// </summary>
    public string ReferenceTypes { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the one customer who may use the coupon, or empty for anyone.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the email address of the one customer who may use the coupon, or empty for anyone.
    /// </summary>
    public string CustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the transaction sources the enabled features register, shown as a hint for <see cref="Sources"/>.
    /// </summary>
    [BindNever]
    public IList<string> KnownSources { get; set; }

    /// <summary>
    /// Gets or sets how many times it has already been redeemed. It is read-only in the editor: a redemption
    /// count is a record of what happened, not a setting.
    /// </summary>
    public int RedemptionCount { get; set; }
}
