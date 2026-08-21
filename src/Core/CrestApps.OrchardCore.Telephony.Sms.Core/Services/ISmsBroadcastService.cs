using CrestApps.OrchardCore.Telephony.Sms.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// Fans a queued <see cref="SmsBroadcast"/> out to individual 1:1 threads, one message per recipient.
/// </summary>
public interface ISmsBroadcastService
{
    /// <summary>
    /// Processes one broadcast: sends to every not-yet-processed recipient, updating counters and marking the
    /// broadcast completed. Safe to resume — already-processed recipients are skipped.
    /// </summary>
    /// <param name="broadcast">The broadcast to process.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProcessAsync(SmsBroadcast broadcast, CancellationToken cancellationToken = default);

    /// <summary>
    /// Picks up every queued or in-progress broadcast and processes it.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}
