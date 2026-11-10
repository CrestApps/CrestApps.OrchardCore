using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;
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

        var previousStatus = broadcast.Status;

        broadcast.SentCount = run.Sent;
        broadcast.FailedCount = run.Failed;
        broadcast.SkippedCount = run.Skipped;
        broadcast.Status = run.Status switch
        {
            OutreachRunStatus.Completed => MessagingBroadcastStatus.Completed,
            OutreachRunStatus.Failed or OutreachRunStatus.Canceled => MessagingBroadcastStatus.Failed,
            _ => MessagingBroadcastStatus.Running,
        };

        // Counted on the update that finishes the broadcast; the engine reports a finished run more than once.
        if (broadcast.Status != previousStatus &&
            broadcast.Status is MessagingBroadcastStatus.Completed or MessagingBroadcastStatus.Failed)
        {
            MessagingTelemetry.RecordBroadcast(
                broadcast.Channel,
                broadcast.Status == MessagingBroadcastStatus.Completed
                    ? MessagingTelemetry.BroadcastResults.Completed
                    : MessagingTelemetry.BroadcastResults.Failed);
        }

        if (run.IsFinished)
        {
            broadcast.CompletedUtc = run.CompletedUtc;
        }

        await _broadcastStore.UpdateAsync(broadcast, cancellationToken);
    }
}
