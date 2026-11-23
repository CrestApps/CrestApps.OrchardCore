using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.MailingLists.WelcomeCoupon;

/// <summary>
/// The Welcome coupon card of the mailing list editor.
/// </summary>
public class MailingListWelcomeCouponViewModel
{
    /// <summary>
    /// Gets or sets whether the list gives a welcome coupon.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the template coupon.
    /// </summary>
    public string CouponId { get; set; }

    /// <summary>
    /// Gets or sets the text each code starts with.
    /// </summary>
    public string Prefix { get; set; }

    /// <summary>
    /// Gets or sets how many days a code works; empty follows the template's end.
    /// </summary>
    public int? ValidForDays { get; set; }

    /// <summary>
    /// Gets or sets when the code is issued.
    /// </summary>
    public MailingListCouponTiming Timing { get; set; }

    /// <summary>
    /// Gets or sets who delivers the code.
    /// </summary>
    public MailingListCouponDelivery Delivery { get; set; }

    /// <summary>
    /// Gets or sets the coupons a code can be issued from.
    /// </summary>
    [BindNever]
    public List<SelectListItem> Coupons { get; set; } = [];
}
