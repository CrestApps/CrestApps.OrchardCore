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
    Task<IReadOnlyCollection<CallbackRequest>> GetDueAsync(DateTime utcNow, int maxCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the open callbacks (pending, promoted or being dialed) reserved for agents that are scheduled in a range,
    /// in order of their scheduled time.
    /// </summary>
    /// <param name="agentIds">The agent profiles whose callbacks to list, or <see langword="null"/> for the callbacks
    /// reserved for any agent.</param>
    /// <param name="fromUtc">The start of the range, in UTC.</param>
    /// <param name="toUtc">The end of the range (exclusive), in UTC.</param>
    /// <param name="maxCount">The maximum number of callbacks to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The callbacks, bounded by <paramref name="maxCount"/>.</returns>
    Task<IReadOnlyCollection<CallbackRequest>> ListOpenForAgentsAsync(
        IEnumerable<string> agentIds,
        DateTime fromUtc,
        DateTime toUtc,
        int maxCount,
        CancellationToken cancellationToken = default);
}
