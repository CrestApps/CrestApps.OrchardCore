namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// The state of an address on a mailing list.
/// </summary>
public enum MailingListMemberStatus
{
    /// <summary>
    /// Signed up and waiting for the confirmation of the address.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Receives the list's e-mails.
    /// </summary>
    Subscribed = 1,

    /// <summary>
    /// Left the list, or was removed from it.
    /// </summary>
    Unsubscribed = 2,

    /// <summary>
    /// Removed because mail to the address bounces.
    /// </summary>
    Cleaned = 3,
}
