using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Outreach;

/// <summary>
/// Copies a broadcast run's counters and state onto the broadcast, so the broadcasts list shows how far it has got.
/// </summary>
public sealed class MessagingBroadcastRunObserver : IOutreachRunObserver
{
    private readonly IMessagingBroadcastStore _broadcastStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingBroadcastRunObserver"/> class.
    /// </summary>
    /// <param name="broadcastStore">The broadcast store.</param>
    public MessagingBroadcastRunObserver(IMessagingBroadcastStore broadcastStore)
    {
        _broadcastStore = broadcastStore;
    }

    /// <inheritdoc/>
    public async Task RunUpdatedAsync(OutreachRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!string.Equals(run.OwnerType, MessagingOutreachConstants.BroadcastOwnerType, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(run.OwnerId))
        {
            return;
        }

        var broadcast = await _broadcastStore.FindByIdAsync(run.OwnerId, cancellationToken);

        if (broadcast is null)
        {
            return;
        }

        broadcast.SentCount = run.Sent;
        broadcast.FailedCount = run.Failed;
        broadcast.SkippedCount = run.Skipped;
        broadcast.Status = run.Status switch
        {
            OutreachRunStatus.Completed => MessagingBroadcastStatus.Completed,
            OutreachRunStatus.Failed or OutreachRunStatus.Canceled => MessagingBroadcastStatus.Failed,
            _ => MessagingBroadcastStatus.Running,
        };

        if (run.IsFinished)
        {
            broadcast.CompletedUtc = run.CompletedUtc;
        }

        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);
    }
}
