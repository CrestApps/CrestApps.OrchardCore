using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for queues.
/// </summary>
public interface IActivityQueueStore : ICatalog<ActivityQueue>
{
    /// <summary>
    /// Finds the queue with the specified unique name.
    /// </summary>
    /// <param name="name">The queue name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching queue, or <see langword="null"/> when none exists.</returns>
    Task<ActivityQueue> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every enabled queue.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled queues.</returns>
    Task<IReadOnlyCollection<ActivityQueue>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
