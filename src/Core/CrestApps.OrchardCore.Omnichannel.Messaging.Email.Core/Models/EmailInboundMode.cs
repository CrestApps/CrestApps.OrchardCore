namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// How mail sent to an email address reaches the workspace.
/// </summary>
public enum EmailInboundMode
{
    /// <summary>
    /// The address only sends. Mail sent to it is not received.
    /// </summary>
    None,

    /// <summary>
    /// The email provider posts each email to the site's inbound email webhook, as SendGrid, Mailgun, Postmark and
    /// Amazon SES can.
    /// </summary>
    Webhook,

    /// <summary>
    /// The site reads the address's mailbox over IMAP, which works with any mail host, including those that cannot
    /// call a webhook.
    /// </summary>
    Mailbox,
}
