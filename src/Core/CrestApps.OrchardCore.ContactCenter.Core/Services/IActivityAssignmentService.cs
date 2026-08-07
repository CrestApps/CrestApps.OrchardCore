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
}
