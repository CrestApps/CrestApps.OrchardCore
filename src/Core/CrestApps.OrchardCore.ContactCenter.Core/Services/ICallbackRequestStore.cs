using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for callback requests.
/// </summary>
public interface ICallbackRequestStore : ICatalog<CallbackRequest>
{
    /// <summary>
    /// Lists pending callbacks that are due at or before the supplied UTC instant.
    /// </summary>
    /// <param name="utcNow">The current UTC instant.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due callbacks ordered by their scheduled time.</returns>
    Task<IReadOnlyCollection<CallbackRequest>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
