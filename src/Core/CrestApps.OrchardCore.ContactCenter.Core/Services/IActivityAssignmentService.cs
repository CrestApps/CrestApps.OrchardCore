using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Assigns queued activities to available agents based on queue membership, priority, and idle time.
/// </summary>
public interface IActivityAssignmentService
{
    /// <summary>
    /// Reserves the next eligible activity in the queue for the longest-idle available agent.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created reservation, or <see langword="null"/> when no work or agent is available.</returns>
    Task<ActivityReservation> AssignNextAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns as many waiting activities as there are available agents in the queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of reservations created.</returns>
    Task<int> AssignQueueAsync(string queueId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reserves a specific waiting activity for a specific agent, bypassing the queue's routing strategy.
    /// Used to route a direct-to-agent (personal line) inbound call. Serialized with the same per-queue
    /// assignment lock as <see cref="AssignNextAsync"/> so it cannot double-reserve the agent or item.
    /// </summary>
    /// <param name="activityItemId">The activity whose queue item is reserved.</param>
    /// <param name="queueId">The queue the item is waiting in.</param>
    /// <param name="agentId">The agent profile to reserve the item for.</param>
    /// <param name="ringTimeoutSeconds">The ring window, in seconds, for a direct-to-agent offer. When null the direct-routing default is used. Ignored for a real queue (the queue's own reservation timeout applies).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The created reservation, or <see langword="null"/> when the agent or item is unavailable.</returns>
    Task<ActivityReservation> AssignSpecificAsync(
        string activityItemId,
        string queueId,
        string agentId,
        int? ringTimeoutSeconds = null,
        CancellationToken cancellationToken = default);
}
