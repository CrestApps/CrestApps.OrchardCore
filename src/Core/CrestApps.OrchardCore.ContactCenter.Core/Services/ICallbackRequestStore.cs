using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for callback requests.
/// </summary>
public interface ICallbackRequestStore : ICatalog<CallbackRequest>
{
    /// <summary>
    /// Lists pending callbacks that are due at or before the supplied UTC instant and are not currently claimed by an unexpired lease.
    /// </summary>
    /// <param name="utcNow">The current UTC instant.</param>
    /// <param name="maxCount">The maximum number of callbacks to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due callbacks ordered by their scheduled time and bounded by <paramref name="maxCount"/>.</returns>
    Task<IReadOnlyCollection<CallbackRequest>> ListDueAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default);
}
