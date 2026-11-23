namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// The welcome coupon code a member was given, kept in the member's properties so a provider that delivers the welcome
/// offer itself (<c>MailingListCouponDelivery.Provider</c>) can write it to the member's profile.
/// </summary>
public sealed class MailingListMemberCoupon
{
    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets when the code stops working (UTC), when it has an end.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }
}
