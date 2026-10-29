using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// How far one address's mailbox has been read, so each pass receives only the mail that arrived since the last one.
/// Kept apart from the address itself, so reading the mailbox never collides with someone editing the address. The
/// item identifier is the address's identifier.
/// </summary>
public sealed class EmailMailboxSyncState : CatalogItem
{
    /// <summary>
    /// Gets or sets the folder's <c>UIDVALIDITY</c> when it was last read. A different value means the server renumbered
    /// the folder, and reading starts again from the newest mail.
    /// </summary>
    public uint UidValidity { get; set; }

    /// <summary>
    /// Gets or sets the highest message UID received from the folder.
    /// </summary>
    public uint LastUid { get; set; }

    /// <summary>
    /// Gets or sets the folder the state belongs to, so pointing the address at another folder starts afresh.
    /// </summary>
    public string Folder { get; set; }

    /// <summary>
    /// Gets or sets the server the state belongs to, so pointing the address at another server starts afresh.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets when the mailbox was last read successfully.
    /// </summary>
    public DateTime? LastSucceededUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last attempt to read the mailbox failed.
    /// </summary>
    public DateTime? LastFailedUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last attempt failed, shown on the address so the operator can fix it.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets how many attempts in a row have failed, which spaces out the next attempts.
    /// </summary>
    public int ConsecutiveFailures { get; set; }
}
