using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// Queues member changes for a list's provider in the provider outbox, in the caller's unit of work.
/// </summary>
public interface IMailingListSyncQueue
{
    /// <summary>
    /// Queues a member change for the list's provider and marks the member as waiting for it, also while the provider is
    /// not registered (the member's sync error then says so). Only a list kept on the site queues nothing, and the
    /// member is marked as synced.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="member">The member as it is now.</param>
    /// <param name="kind">What the provider is asked to do.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns><see langword="true"/> when a change was queued.</returns>
    Task<bool> QueueAsync(MailingList list, MailingListMember member, MailingListPushKind kind, CancellationToken cancellationToken = default);
}
