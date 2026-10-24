namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

/// <summary>
/// What happens to an email in the mailbox once the workspace has received it.
/// </summary>
public enum EmailMailboxAfterProcessing
{
    /// <summary>
    /// The email is marked as read and left where it is.
    /// </summary>
    MarkAsRead,

    /// <summary>
    /// The email is marked as read and moved to a folder, which keeps the inbox to what has not been received yet.
    /// </summary>
    MoveToFolder,
}
