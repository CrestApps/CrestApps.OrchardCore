namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// The welcome coupon a mailing list gives each new address once, kept in the list's properties by the welcome coupon
/// feature. A code of the template coupon is issued per list and address, never twice.
/// </summary>
public sealed class MailingListWelcomeCoupon
{
    /// <summary>
    /// Gets or sets whether the list gives a welcome coupon.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the id of the template coupon the codes are issued from (a code coupon, not an automatic one).
    /// </summary>
    public string CouponId { get; set; }

    /// <summary>
    /// Gets or sets the text each code starts with.
    /// </summary>
    public string Prefix { get; set; } = "WELCOME-";

    /// <summary>
    /// Gets or sets how many days a code works after it is issued; <see langword="null"/> follows the template's end.
    /// </summary>
    public int? ValidForDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets when the code is issued.
    /// </summary>
    public MailingListCouponTiming Timing { get; set; } = MailingListCouponTiming.AfterVerification;

    /// <summary>
    /// Gets or sets who delivers the code.
    /// </summary>
    public MailingListCouponDelivery Delivery { get; set; } = MailingListCouponDelivery.Email;
}
