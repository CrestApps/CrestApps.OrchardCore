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

    /// <summary>
    /// Gives up on the leg of the agent claimed for an answered over-dialed call when it has not answered: the leg is hung
    /// up, the agent released back to work, and the person given the abandoned-call message. Does nothing once the agent
    /// has joined, the call has ended or been abandoned, or no agent was claimed, so it is safe to race the answer.
    /// </summary>
    /// <param name="interactionId">The interaction of the answered call.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the call was given up on.</returns>
    Task<bool> ReleaseUnansweredAgentLegAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles every over-dialed call a person answered that nothing has connected or abandoned well after the answer:
    /// one never claimed for an agent is abandoned with the message, and one whose claimed agent never joined is given up
    /// on as <see cref="ReleaseUnansweredAgentLegAsync"/> does. It is the backstop for a connect or a deadline lost with
    /// the node that held it.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of calls settled.</returns>
    Task<int> SweepAnsweredUnconnectedAsync(CancellationToken cancellationToken = default);
}
