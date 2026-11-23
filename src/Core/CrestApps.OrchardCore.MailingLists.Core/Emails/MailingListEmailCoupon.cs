namespace CrestApps.OrchardCore.MailingLists.Core.Emails;

/// <summary>
/// The welcome coupon an e-mail carries, as the site's template reads it: <c>{{ Coupon.Code }}</c>, <c>{{ Coupon.Url }}</c>
/// (the link that applies the code at checkout), <c>{{ Coupon.Description }}</c> and <c>{{ Coupon.Expires }}</c> (in the
/// site's time zone).
/// </summary>
public sealed class MailingListEmailCoupon
{
    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the link that applies the code at checkout.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the discount in words.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets when the code stops working, written in the site's time zone; empty when it has no end.
    /// </summary>
    public string Expires { get; set; }
}
