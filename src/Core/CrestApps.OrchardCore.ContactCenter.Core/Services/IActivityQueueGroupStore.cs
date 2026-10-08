using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for queue groups.
/// </summary>
public interface IActivityQueueGroupStore : ICatalog<ActivityQueueGroup>
{
    /// <summary>
    /// Finds the queue group with the specified unique name.
    /// </summary>
    /// <param name="name">The queue-group name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching queue group, or <see langword="null"/> when none exists.</returns>
    Task<ActivityQueueGroup> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
