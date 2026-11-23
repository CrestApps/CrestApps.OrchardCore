using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.MailingLists.Core.Sync;

/// <summary>
/// Queues member changes in the provider outbox (a leased work queue saved in the caller's session), and asks for each
/// change to be sent right after the request commits; the outbox background task sends whatever is still due. Every
/// change of a list that is not kept on the site is queued, also while the list's provider is not registered.
/// </summary>
public sealed class MailingListSyncQueue : IMailingListSyncQueue
{
    private readonly IMailingListProviderResolver _providers;
    private readonly ILeasedWorkQueue<MailingListSyncItem> _queue;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListSyncQueue"/> class.
    /// </summary>
    /// <param name="providers">The provider resolver.</param>
    /// <param name="queue">The provider outbox.</param>
    /// <param name="clock">The clock.</param>
    public MailingListSyncQueue(
        IMailingListProviderResolver providers,
        ILeasedWorkQueue<MailingListSyncItem> queue,
        IClock clock)
    {
        _providers = providers;
        _queue = queue;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> QueueAsync(MailingList list, MailingListMember member, MailingListPushKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(member);

        // Only a list kept on the site is synced without a push. A list whose provider is not registered (its feature is
        // off) is not: its changes wait in the outbox until the provider is back.
        if (list.IsLocal())
        {
            member.SyncState = MailingListSyncState.Synced;
            member.SyncError = null;

            return false;
        }

        var item = new MailingListSyncItem
        {
            ListId = list.ItemId,
            MemberId = member.ItemId,
            Kind = kind,
            Email = kind == MailingListPushKind.Erase ? member.NormalizedEmail : null,
            ProviderMemberId = kind == MailingListPushKind.Erase ? member.ProviderMemberId : null,
            CreatedUtc = _clock.UtcNow,
        };

        await _queue.EnqueueAsync(item, cancellationToken);

        var providerName = list.GetProviderName();

        member.SyncState = MailingListSyncState.Pending;
        member.SyncError = _providers.Find(providerName) is null ? MailingListSyncWaits.ProviderMissing(providerName) : null;

        var itemId = item.ItemId;

        if (ShellScope.Current is not null)
        {
            // Sent after the request commits, so the provider never hears of a change that was rolled back.
            ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<MailingListSyncProcessor>().RunAsync(itemId));
        }

        return true;
    }
}
