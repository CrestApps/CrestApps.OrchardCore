using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Follows a call an automated dialer profile placed from the moment a person answers it: it records the answer and
/// whether an agent reached the person in time, and when no agent can, plays the profile's abandoned-call message to
/// the person before the call is ended.
/// </summary>
/// <remarks>
/// <para>
/// Every fact is filed in the durable event log under the dialer profile (<c>DialerLiveAnswered</c> and
/// <c>DialerCallAbandoned</c>), which is what the profile's rolling abandonment rate is counted from. A call is counted
/// once whatever path reports it, and only for Power, Progressive and Predictive profiles: in Preview the agent is on
/// the call before it is placed.
/// </para>
/// <para>
/// The methods change the interaction's technical metadata and return <see langword="true"/> when they did; the caller
/// saves the interaction.
/// </para>
/// </remarks>
public interface IDialerAbandonmentTracker
{
    /// <summary>
    /// Records that a person, not a machine, answered the call.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="answeredUtc">When the dialer learned a person answered.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the interaction was changed.</returns>
    Task<bool> RecordLiveAnswerAsync(Interaction interaction, DateTime answeredUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an agent was connected to the call, which abandons it when that came later than
    /// <see cref="DialerAbandonment.ConnectThreshold"/> after the person answered.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="connectedUtc">When the agent was connected.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the interaction was changed.</returns>
    Task<bool> RecordAgentConnectedAsync(Interaction interaction, DateTime connectedUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a call a person answered ended before any agent was connected, which abandons it when the person
    /// had waited longer than <see cref="DialerAbandonment.ConnectThreshold"/>.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="endedUtc">When the call ended.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the interaction was changed.</returns>
    Task<bool> RecordEndedWithoutAgentAsync(Interaction interaction, DateTime endedUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abandons a call a person answered that no agent can be connected to, and starts the profile's abandoned-call
    /// message on the person's leg; the provider ends the call once the message has been spoken, and a safety hang-up
    /// ends it if the provider never reports that it was.
    /// </summary>
    /// <param name="interaction">The interaction of the call.</param>
    /// <param name="providerName">The provider carrying the call.</param>
    /// <param name="providerCallId">The provider's identifier for the person's leg.</param>
    /// <param name="reason">Why no agent can be connected, one of <see cref="DialerAbandonment.Reasons"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when the message was started and the provider now owns ending the call;
    /// <see langword="false"/> when the call is not an automated dialer call or there is no message to play, and the
    /// caller releases the person as it always did.
    /// </returns>
    Task<bool> AbandonAsync(
        Interaction interaction,
        string providerName,
        string providerCallId,
        string reason,
        CancellationToken cancellationToken = default);
}
