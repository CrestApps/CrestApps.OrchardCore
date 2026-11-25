using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Reads and records a broadcast's progress. The outreach run that sends a broadcast counts a message the channel held
/// back as sent, because the channel accepted it, and records it as held (<see cref="MessagingBroadcast.QueuedCount"/>).
/// What the outbox later did with the held messages is kept apart, in <see cref="MessagingBroadcastQueueOutcomes"/>,
/// because the outbox and the run save at the same time and each would overwrite the other's counts on one document.
/// The progress is read from both, so a held message is never shown as sent.
/// </summary>
public static class MessagingBroadcastExtensions
{
    /// <summary>
    /// Gets how many of the broadcast's held-back messages still wait in the outbox.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <param name="outcome">What the outbox did with the held messages, or <see langword="null"/> when nothing yet.</param>
    /// <returns>The held messages less those the outbox sent or gave up on.</returns>
    public static int GetQueuedCount(this MessagingBroadcast broadcast, MessagingBroadcastQueueOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return Math.Max(0, broadcast.QueuedCount - (outcome?.Sent ?? 0) - (outcome?.Failed ?? 0));
    }

    /// <summary>
    /// Gets how many recipients the message was actually sent to.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <param name="outcome">What the outbox did with the held messages, or <see langword="null"/> when nothing yet.</param>
    /// <returns>The run's sent count, less the messages still queued and the queued ones that failed later.</returns>
    public static int GetSentCount(this MessagingBroadcast broadcast, MessagingBroadcastQueueOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return Math.Max(0, broadcast.SentCount - broadcast.GetQueuedCount(outcome) - (outcome?.Failed ?? 0));
    }

    /// <summary>
    /// Gets how many recipients the message could not be sent to, at once or after being queued.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <param name="outcome">What the outbox did with the held messages, or <see langword="null"/> when nothing yet.</param>
    /// <returns>The number of failed recipients.</returns>
    public static int GetFailedCount(this MessagingBroadcast broadcast, MessagingBroadcastQueueOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return broadcast.FailedCount + (outcome?.Failed ?? 0);
    }

    /// <summary>
    /// Gets the status the broadcast is shown with: one whose recipients were all processed while some of its messages
    /// still wait to be sent is still sending, not completed.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    /// <param name="outcome">What the outbox did with the held messages, or <see langword="null"/> when nothing yet.</param>
    /// <returns>The status to show.</returns>
    public static MessagingBroadcastStatus GetDisplayStatus(this MessagingBroadcast broadcast, MessagingBroadcastQueueOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        return broadcast.Status == MessagingBroadcastStatus.Completed && broadcast.GetQueuedCount(outcome) > 0
            ? MessagingBroadcastStatus.Running
            : broadcast.Status;
    }

    /// <summary>
    /// Records a message the channel held back for one recipient: it is queued, not sent. Only the broadcast's run calls
    /// it.
    /// </summary>
    /// <param name="broadcast">The broadcast.</param>
    public static void RecordQueued(this MessagingBroadcast broadcast)
    {
        ArgumentNullException.ThrowIfNull(broadcast);

        broadcast.QueuedCount++;
    }

    /// <summary>
    /// Records what became of a broadcast's queued messages: how many the outbox sent and how many it gave up on. Only
    /// the outbox calls it.
    /// </summary>
    /// <param name="outcomes">The outcomes of every broadcast.</param>
    /// <param name="broadcastId">The broadcast.</param>
    /// <param name="sent">The number of queued messages that were sent.</param>
    /// <param name="failed">The number of queued messages that failed for good.</param>
    public static void RecordQueuedOutcome(this MessagingBroadcastQueueOutcomes outcomes, string broadcastId, int sent, int failed)
    {
        ArgumentNullException.ThrowIfNull(outcomes);
        ArgumentException.ThrowIfNullOrEmpty(broadcastId);
        ArgumentOutOfRangeException.ThrowIfNegative(sent);
        ArgumentOutOfRangeException.ThrowIfNegative(failed);

        outcomes.Broadcasts ??= new(StringComparer.Ordinal);

        if (!outcomes.Broadcasts.TryGetValue(broadcastId, out var outcome))
        {
            outcome = new MessagingBroadcastQueueOutcome();
            outcomes.Broadcasts[broadcastId] = outcome;
        }

        outcome.Sent += sent;
        outcome.Failed += failed;
    }

    /// <summary>
    /// Gets what the outbox did with one broadcast's held messages.
    /// </summary>
    /// <param name="outcomes">The outcomes of every broadcast, or <see langword="null"/>.</param>
    /// <param name="broadcastId">The broadcast.</param>
    /// <returns>The outcome, or <see langword="null"/> when the outbox has settled none of its messages.</returns>
    public static MessagingBroadcastQueueOutcome Find(this MessagingBroadcastQueueOutcomes outcomes, string broadcastId)
        => outcomes?.Broadcasts is { } broadcasts && !string.IsNullOrEmpty(broadcastId) && broadcasts.TryGetValue(broadcastId, out var outcome)
            ? outcome
            : null;
}
