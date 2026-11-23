namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// Whether a member's last change reached the list's provider.
/// </summary>
public enum MailingListSyncState
{
    /// <summary>
    /// The provider has the member's last change, or the list is kept on the site.
    /// </summary>
    Synced = 0,

    /// <summary>
    /// The change waits in the provider outbox, also while the provider's feature is off or the provider is not set up;
    /// the member's sync error then says why.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Every attempt to send the change failed; an admin must look.
    /// </summary>
    Failed = 2,
}
