using CrestApps.Core.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.WorkQueues;

namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// One member change waiting to reach a list's provider, kept in the provider outbox. It is queued in the unit of work
/// that changed the member, so it exists only when that change committed.
/// </summary>
public sealed class MailingListSyncItem : CatalogItem, ILeasedWorkItem
{
    /// <summary>
    /// Gets or sets the id of the list.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the id of the member.
    /// </summary>
    public string MemberId { get; set; }

    /// <summary>
    /// Gets or sets what the provider is asked to do.
    /// </summary>
    public MailingListPushKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the address, kept only for an erasure: the member is deleted by then, and the provider needs the
    /// address to find its own record. The item is removed once the provider took it.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the member's id at the provider, kept only for an erasure.
    /// </summary>
    public string ProviderMemberId { get; set; }

    /// <summary>
    /// Gets or sets when the change was queued (UTC).
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc/>
    public LeasedWorkItemStatus Status { get; set; }

    /// <inheritdoc/>
    public string OwnerToken { get; set; }

    /// <inheritdoc/>
    public long FenceToken { get; set; }

    /// <inheritdoc/>
    public int AttemptCount { get; set; }

    /// <inheritdoc/>
    public DateTime NextAttemptUtc { get; set; }

    /// <inheritdoc/>
    public string LastError { get; set; }
}
