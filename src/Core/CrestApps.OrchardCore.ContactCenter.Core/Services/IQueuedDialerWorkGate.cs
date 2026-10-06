using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides, before routing reserves an agent for it, whether a waiting campaign record may be dialed now.
/// </summary>
/// <remarks>
/// The retry cool-down and the attempt limit used to be checked only after an agent had been reserved for the record,
/// so a record that could not be dialed yet was still offered -- and popped on the agent's screen -- before being
/// suppressed. Checking them first keeps such a record away from every agent.
/// </remarks>
public interface IQueuedDialerWorkGate
{
    /// <summary>
    /// Holds back a waiting campaign record that may not be dialed now: one scheduled for later or still cooling down
    /// after its last attempt goes to the back of its queue, and one that has used every attempt its dialer profile
    /// allows is taken out of the queue and completed by the dialer.
    /// </summary>
    /// <param name="queueItem">The waiting item routing is about to offer.</param>
    /// <param name="utcNow">The current time.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the item was held back and routing should look at the next one.</returns>
    Task<bool> TryHoldBackAsync(QueueItem queueItem, DateTime utcNow, CancellationToken cancellationToken = default);
}
