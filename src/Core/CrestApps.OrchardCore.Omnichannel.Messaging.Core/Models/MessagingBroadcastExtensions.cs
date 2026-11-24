using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Reads and records a broadcast's progress. The outreach run that sends a broadcast counts a message the channel held
/// back as sent, because the channel accepted it; the broadcast keeps those apart as queued until the outbox actually
/// sends them, or gives up on them, so it never shows a held message as sent.
/// </summary>
public static class MessagingBroadcastExtensions
{
    /// <summary>
    /// Gets how many recipients the message was actually sent to.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <returns>The run's sent count, less the messages still queued and the queued ones that failed later.</returns>
    public static int GetSentCount(this MessagingBroadcast broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return Math.Max(0, broadcast.SentCount - broadcast.QueuedCount - broadcast.QueuedFailedCount);
    }

    /// <summary>
    /// Gets how many recipients the message could not be sent to, at once or after being queued.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <returns>The number of failed recipients.</returns>
    public static int GetFailedCount(this MessagingBroadcast broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return broadcast.FailedCount + broadcast.QueuedFailedCount;
    }

    /// <summary>
    /// Gets the status the broadcast is shown with: one whose recipients were all processed while some of its messages
    /// still wait to be sent is still sending, not completed.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <returns>The status to show.</returns>
    public static MessagingBroadcastStatus GetDisplayStatus(this MessagingBroadcast broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return broadcast.Status == MessagingBroadcastStatus.Completed && broadcast.QueuedCount > 0
            ? MessagingBroadcastStatus.Running
            : broadcast.Status;
    }

    /// <summary>
    /// Records a message the channel held back for one recipient: it is queued, not sent.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    public static void RecordQueued(this MessagingBroadcast broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        broadcast.QueuedCount++;
    }

    /// <summary>
    /// Records what became of queued messages: how many the outbox sent and how many it gave up on.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <param name="sent">The number of queued messages that were sent.</param>
    /// <param name="failed">The number of queued messages that failed for good.</param>
    public static void RecordQueuedOutcome(this MessagingBroadcast broadcast, int sent, int failed)
    {
        ArgumentNullException.ThrowIfNull(broadcast);
        ArgumentOutOfRangeException.ThrowIfNegative(sent);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);

        broadcast.QueuedCount = Math.Max(0, broadcast.QueuedCount - sent - failed);
        broadcast.QueuedFailedCount += failed;
    }
}
