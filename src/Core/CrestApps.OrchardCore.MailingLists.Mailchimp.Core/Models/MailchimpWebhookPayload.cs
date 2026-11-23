namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// What the provider webhook inbox keeps of one Mailchimp webhook: the site's list it was sent for, the body exactly as
/// Mailchimp sent it, and when it arrived.
/// </summary>
public sealed class MailchimpWebhookPayload
{
    /// <summary>
    /// Gets or sets the id of the mailing list on the site, from the webhook URL.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the form-encoded body Mailchimp sent.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets when the site received the webhook (UTC), used when the body has no <c>fired_at</c>.
    /// </summary>
    public DateTime ReceivedUtc { get; set; }
}
