namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// The mailbox an email address's mail is read from over IMAP.
/// </summary>
public sealed class EmailMailboxSettings
{
    /// <summary>
    /// The folder read when none is set.
    /// </summary>
    public const string DefaultFolder = "INBOX";

    /// <summary>
    /// Gets or sets the IMAP server.
    /// </summary>
    public EmailServerSettings Server { get; set; } = new();

    /// <summary>
    /// Gets or sets the folder new mail is read from.
    /// </summary>
    public string Folder { get; set; } = DefaultFolder;

    /// <summary>
    /// Gets or sets what happens to an email once it is received.
    /// </summary>
    public EmailMailboxAfterProcessing AfterProcessing { get; set; }

    /// <summary>
    /// Gets or sets the folder a received email is moved to, when <see cref="AfterProcessing"/> moves it.
    /// </summary>
    public string ProcessedFolder { get; set; }

    /// <summary>
    /// Gets or sets how many days of unread mail are received the first time the mailbox is read. Zero receives only
    /// mail that arrives after the mailbox is connected, so connecting a busy mailbox does not flood the workspace.
    /// </summary>
    public int InitialLookbackDays { get; set; }
}
