namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// How one of the business's email addresses sends and receives, kept on the address (the Omnichannel address
/// record) so each mailbox can use its own provider.
/// </summary>
public sealed class EmailAddressSettings
{
    /// <summary>
    /// Gets or sets the name shown as the sender, such as <c>Contoso Support</c>.
    /// </summary>
    public string SenderName { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the transport the address sends through (see <c>IEmailTransport</c>).
    /// Empty uses Orchard Core's email service.
    /// </summary>
    public string TransportName { get; set; }

    /// <summary>
    /// Gets or sets the Orchard Core email provider the address sends through, when the transport is Orchard Core's
    /// email service. Empty uses the tenant's default provider.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the SMTP server the address sends through, when the transport is the address's own SMTP server.
    /// </summary>
    public EmailServerSettings Smtp { get; set; } = new();

    /// <summary>
    /// Gets or sets the subject of a new email that is not a reply and was given none.
    /// </summary>
    public string DefaultSubject { get; set; }

    /// <summary>
    /// Gets or sets the signature added below every email the address sends.
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether bulk and outreach email (broadcasts, campaign openers, follow-ups)
    /// carries an unsubscribe link and the one-click unsubscribe header mailbox providers require of bulk senders.
    /// </summary>
    public bool IncludeUnsubscribeLink { get; set; } = true;

    /// <summary>
    /// Gets or sets how mail sent to the address reaches the workspace.
    /// </summary>
    public EmailInboundMode InboundMode { get; set; }

    /// <summary>
    /// Gets or sets the mailbox read over IMAP, when <see cref="InboundMode"/> is <see cref="EmailInboundMode.Mailbox"/>.
    /// </summary>
    public EmailMailboxSettings Mailbox { get; set; } = new();
}
