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

    /// <summary>
    /// Lists the pacing records of the campaign queues a dialer profile last paced, most recent cycle first.
    /// </summary>
    /// <param name="dialerProfileId">The dialer profile.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The records; empty when the profile has never over-dialed.</returns>
    Task<IReadOnlyCollection<PredictivePacingState>> GetByDialerProfileIdAsync(string dialerProfileId, CancellationToken cancellationToken = default);
}
