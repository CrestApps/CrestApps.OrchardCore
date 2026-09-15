using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Converts a caller who is waiting in a queue into a callback that keeps their place in line.
/// </summary>
public interface IQueuedCallbackService
{
    /// <summary>
    /// Accepts a queued callback for a waiting caller.
    /// </summary>
    /// <param name="item">The queue item the caller is waiting on.</param>
    /// <param name="destination">The number to call back.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the callback was scheduled and the caller removed from the queue.</returns>
    Task<bool> AcceptAsync(QueueItem item, string destination, CancellationToken cancellationToken = default);
}
