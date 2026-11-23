namespace CrestApps.OrchardCore.MailingLists.Core.Emails;

/// <summary>
/// The links an e-mail carries, as the site's template reads them: <c>{{ Links.Verify }}</c> (the confirmation or claim
/// link, when the e-mail has one) and <c>{{ Links.Unsubscribe }}</c>.
/// </summary>
public sealed class MailingListEmailLinks
{
    /// <summary>
    /// Gets or sets the link that confirms the sign-up or claims the welcome coupon.
    /// </summary>
    public string Verify { get; set; }

    /// <summary>
    /// Gets or sets the link that unsubscribes the address from the list.
    /// </summary>
    public string Unsubscribe { get; set; }
}
