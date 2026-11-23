namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// Who delivers a mailing list's welcome coupon.
/// </summary>
public enum MailingListCouponDelivery
{
    /// <summary>
    /// The site e-mails the code.
    /// </summary>
    Email = 0,

    /// <summary>
    /// The code is written to the member's profile at the list's provider, whose own automation sends it.
    /// </summary>
    Provider = 1,
}
