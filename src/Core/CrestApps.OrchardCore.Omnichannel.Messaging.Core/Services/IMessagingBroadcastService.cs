using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Fans a queued <see cref="MessagingBroadcast"/> out to individual 1:1 threads, one message per recipient.
/// </summary>
public interface IMessagingBroadcastService
{
    /// <summary>
    /// Processes one broadcast: sends to every not-yet-processed recipient, updating counters and marking the
    /// broadcast completed. Safe to resume — already-processed recipients are skipped.
    /// </summary>
    /// <param name="broadcast">The broadcast to process.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProcessAsync(MessagingBroadcast broadcast, CancellationToken cancellationToken = default);

    /// <summary>
    /// Picks up every queued or in-progress broadcast and processes it.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}
