namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// The Mailchimp settings of one mailing list, kept in the list's properties and edited on its Mailchimp card. Whether
/// Mailchimp sends the confirmation e-mail is not kept here: it is the list's own confirmation setting
/// (<c>MailingListConfirmation.Provider</c>).
/// </summary>
public sealed class MailchimpListSettings
{
    /// <summary>
    /// Gets or sets the id of the Mailchimp audience (list) the members are kept in.
    /// </summary>
    public string AudienceId { get; set; }

    /// <summary>
    /// Gets or sets the tags added to every member pushed to the audience, such as <c>website-signup</c>.
    /// </summary>
    public string[] Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the member's name is sent in the <c>FNAME</c> and <c>LNAME</c> merge fields.
    /// </summary>
    public bool SendNameMergeFields { get; set; } = true;

    /// <summary>
    /// Gets or sets the tag of the text merge field the welcome coupon code is written to, when the list's welcome
    /// coupon is delivered by the provider.
    /// </summary>
    public string CouponMergeTag { get; set; } = MailchimpConstants.DefaultCouponMergeTag;

    /// <summary>
    /// Gets or sets the id of the webhook registered on the audience, when one is registered.
    /// </summary>
    public string WebhookId { get; set; }

    /// <summary>
    /// Gets or sets the hard-to-guess value in the webhook URL, protected with
    /// <see cref="MailchimpConstants.WebhookSecretPurpose"/>.
    /// </summary>
    public string WebhookUrlSecret { get; set; }

    /// <summary>
    /// Gets or sets the secret Mailchimp signs the webhook's requests with, protected with
    /// <see cref="MailchimpConstants.WebhookSecretPurpose"/>; empty when Mailchimp returned none.
    /// </summary>
    public string WebhookSigningSecret { get; set; }

    /// <summary>
    /// Gets or sets when the webhook was registered (UTC).
    /// </summary>
    public DateTime? WebhookRegisteredUtc { get; set; }

    /// <summary>
    /// Returns whether a webhook is registered for the list.
    /// </summary>
    public bool HasWebhook()
        => !string.IsNullOrEmpty(WebhookId) && !string.IsNullOrEmpty(WebhookUrlSecret);

    /// <summary>
    /// Forgets the registered webhook and its secrets, for example when the audience changes.
    /// </summary>
    public void ClearWebhook()
    {
        WebhookId = null;
        WebhookUrlSecret = null;
        WebhookSigningSecret = null;
        WebhookRegisteredUtc = null;
    }
}
