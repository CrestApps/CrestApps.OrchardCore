using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;

/// <summary>
/// Edits how one email address sends and receives.
/// </summary>
public class EmailAddressSettingsViewModel
{
    /// <summary>
    /// Gets or sets the name shown as the sender.
    /// </summary>
    public string SenderName { get; set; }

    /// <summary>
    /// Gets or sets the transport the address sends through.
    /// </summary>
    public string TransportName { get; set; }

    /// <summary>
    /// Gets or sets the Orchard Core email provider, when the transport is Orchard Core's email service.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the SMTP server host.
    /// </summary>
    public string SmtpHost { get; set; }

    /// <summary>
    /// Gets or sets the SMTP server port; zero for the default.
    /// </summary>
    public int SmtpPort { get; set; }

    /// <summary>
    /// Gets or sets how the SMTP connection is secured.
    /// </summary>
    public EmailConnectionSecurity SmtpSecurity { get; set; }

    /// <summary>
    /// Gets or sets the SMTP user name.
    /// </summary>
    public string SmtpUserName { get; set; }

    /// <summary>
    /// Gets or sets a new SMTP password; empty keeps the stored one.
    /// </summary>
    public string SmtpPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an SMTP password is stored.
    /// </summary>
    [BindNever]
    public bool HasSmtpPassword { get; set; }

    /// <summary>
    /// Gets or sets the subject of a new email that is not a reply.
    /// </summary>
    public string DefaultSubject { get; set; }

    /// <summary>
    /// Gets or sets the signature added below every email.
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether bulk and outreach email carries an unsubscribe link.
    /// </summary>
    public bool IncludeUnsubscribeLink { get; set; }

    /// <summary>
    /// Gets or sets how mail sent to the address reaches the workspace.
    /// </summary>
    public EmailInboundMode InboundMode { get; set; }

    /// <summary>
    /// Gets or sets the IMAP server host.
    /// </summary>
    public string ImapHost { get; set; }

    /// <summary>
    /// Gets or sets the IMAP server port; zero for the default.
    /// </summary>
    public int ImapPort { get; set; }

    /// <summary>
    /// Gets or sets how the IMAP connection is secured.
    /// </summary>
    public EmailConnectionSecurity ImapSecurity { get; set; }

    /// <summary>
    /// Gets or sets the IMAP user name.
    /// </summary>
    public string ImapUserName { get; set; }

    /// <summary>
    /// Gets or sets a new IMAP password; empty keeps the stored one.
    /// </summary>
    public string ImapPassword { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an IMAP password is stored.
    /// </summary>
    [BindNever]
    public bool HasImapPassword { get; set; }

    /// <summary>
    /// Gets or sets the folder new mail is read from.
    /// </summary>
    public string MailboxFolder { get; set; }

    /// <summary>
    /// Gets or sets what happens to an email once it is received.
    /// </summary>
    public EmailMailboxAfterProcessing AfterProcessing { get; set; }

    /// <summary>
    /// Gets or sets the folder a received email is moved to.
    /// </summary>
    public string ProcessedFolder { get; set; }

    /// <summary>
    /// Gets or sets how many days of unread mail the first read receives.
    /// </summary>
    public int InitialLookbackDays { get; set; }

    /// <summary>
    /// Gets or sets the address's identifier, for the connection test.
    /// </summary>
    [BindNever]
    public string AddressId { get; set; }

    /// <summary>
    /// Gets or sets the last time the mailbox was read successfully.
    /// </summary>
    [BindNever]
    public DateTime? MailboxLastSucceededUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last read of the mailbox failed, when it did.
    /// </summary>
    [BindNever]
    public string MailboxLastError { get; set; }

    /// <summary>
    /// Gets or sets the inbound webhook URLs, by provider, for the webhook mode's instructions.
    /// </summary>
    [BindNever]
    public IReadOnlyList<KeyValuePair<string, string>> WebhookUrls { get; set; } = [];

    /// <summary>
    /// Gets or sets the sending transports offered.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> Transports { get; set; } = [];

    /// <summary>
    /// Gets or sets the enabled Orchard Core email providers.
    /// </summary>
    [BindNever]
    public IReadOnlyList<SelectListItem> Providers { get; set; } = [];
}
