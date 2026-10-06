using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Connects a person who answered an over-dialed call to an agent: it claims the free agent routing prefers and bridges
/// them in, or, when nobody is free, plays the profile's abandoned-call message and records the call abandoned.
/// </summary>
public interface IPredictiveAgentConnector
{
    /// <summary>
    /// Connects the answered over-dialed call to a free agent, or abandons it. Safe to call more than once for the same
    /// call, from any node: only the first call that finds it unclaimed and unabandoned acts.
    /// </summary>
    /// <param name="interactionId">The interaction of the answered call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What happened to the call.</returns>
    Task<PredictiveConnectOutcome> ConnectAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects every answered over-dialed call of the campaign queue that is still waiting for an agent: one waiting
    /// out the profile's connect wait, or one whose connect was lost (a node that stopped between the answer and the
    /// connect). An agent who becomes free takes these before any new call is placed.
    /// </summary>
    /// <param name="queueId">The campaign queue.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of calls claimed or abandoned.</returns>
    Task<int> ServiceWaitingAsync(string queueId, CancellationToken cancellationToken = default);
}
