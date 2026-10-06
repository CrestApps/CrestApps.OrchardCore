using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Runs one over-dial pacing cycle of a campaign queue: it measures the queue, decides how many calls to place without an
/// agent, places them, and records the decision.
/// </summary>
public interface IPredictiveOverDialPacer
{
    /// <summary>
    /// Runs one cycle for the campaign queue under the queue's pacing lock. The cycle commits its pacing record, under a
    /// concurrency check, together with the calls it staged; the calls are only placed once that commit succeeds. Any
    /// doubt -- a statistic missing, the lock held, a conflict with another node, an error -- places no call without an
    /// agent.
    /// </summary>
    /// <param name="profile">The over-dialing Predictive profile the queue is dialed with.</param>
    /// <param name="queueId">The campaign queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What the cycle decided and placed.</returns>
    Task<PredictivePacingCycleResult> RunCycleAsync(DialerProfile profile, string queueId, CancellationToken cancellationToken = default);
}
