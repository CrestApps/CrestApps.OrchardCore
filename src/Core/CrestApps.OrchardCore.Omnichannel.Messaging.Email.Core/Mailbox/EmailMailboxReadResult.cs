namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Mailbox;

/// <summary>
/// What one read of a mailbox found.
/// </summary>
public sealed class EmailMailboxReadResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the mailbox was read.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets how many emails were received.
    /// </summary>
    public int Received { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether more mail is waiting than one read takes.
    /// </summary>
    public bool HasMore { get; set; }

    /// <summary>
    /// Gets or sets why the read failed.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets the inbox records of the emails the read committed, which are processed once the read is over.
    /// </summary>
    public IList<string> InboxMessageIds { get; } = [];
}
