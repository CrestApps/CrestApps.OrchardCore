using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Stores the pacing record of each over-dialing campaign queue, written under a concurrency check.
/// </summary>
public interface IPredictivePacingStateStore : ICatalog<PredictivePacingState>
{
    /// <summary>
    /// Finds the pacing record of a campaign queue.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The record, or <see langword="null"/> when the queue has never been over-dialed.</returns>
    Task<PredictivePacingState> FindByQueueIdAsync(string queueId, CancellationToken cancellationToken = default);
}
