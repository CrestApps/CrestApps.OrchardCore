using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Core.Indexes;

/// <summary>
/// Indexes mailing list members by list, address, state and how they joined.
/// </summary>
public sealed class MailingListMemberIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the member's document, which a whole list is paged through in the order of.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the id of the list.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the normalized e-mail address.
    /// </summary>
    public string NormalizedEmail { get; set; }

    /// <summary>
    /// Gets or sets the member's state.
    /// </summary>
    public MailingListMemberStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who signed up, when there was one.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets when the member was last subscribed (UTC).
    /// </summary>
    public DateTime? SubscribedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the member's state last changed (UTC); the retention of unconfirmed sign-ups ages on it.
    /// </summary>
    public DateTime StatusChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets how the member joined.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets whether the member's last change reached the list's provider.
    /// </summary>
    public MailingListSyncState SyncState { get; set; }
}
