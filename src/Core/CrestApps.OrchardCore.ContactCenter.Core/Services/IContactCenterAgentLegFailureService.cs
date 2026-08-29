namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reconciles the agent leg of an outbound call against its interaction after the platform has originated it.
/// </summary>
/// <remarks>
/// A provider that delivers a call to a soft phone by originating a second leg reports that leg's lifecycle on
/// the leg's own identifier, which belongs to no interaction, so those events are discarded by normalization.
/// That leaves two records wrong. When the leg fails, nothing ends the call: the customer stays connected to an
/// agent who was never reached, the agent stays "on a call" and is offered no further work, and recovery skips
/// the record because recovery never touches an unsettled interaction. When the leg answers, its
/// <c>call.answered</c> is dropped too, so the agent leg recorded on the call topology never advances past
/// dialing and ends up marked failed with no answered time, misreporting who was on the call and its talk time.
/// This service is the path both outcomes take instead, correlating on the customer call the agent leg was
/// joining (which the leg carries in its client state).
/// </remarks>
public interface IContactCenterAgentLegFailureService
{
    /// <summary>
    /// Records that the agent leg answered, advancing the agent leg already on the call topology to an answered
    /// state so the call correctly reports the agent was on it.
    /// </summary>
    /// <param name="providerName">The technical name of the provider that reported the answer.</param>
    /// <param name="peerProviderCallId">The provider identifier of the customer call the agent leg was joining.</param>
    /// <param name="agentLegProviderCallId">The provider identifier of the agent leg the platform originated.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a live agent leg was advanced; otherwise <see langword="false"/>.</returns>
    Task<bool> RecordAnsweredAsync(
        string providerName,
        string peerProviderCallId,
        string agentLegProviderCallId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Settles the call whose agent leg failed, identified by the call the agent leg was being connected to.
    /// </summary>
    /// <param name="providerName">The technical name of the provider that reported the failure.</param>
    /// <param name="peerProviderCallId">The provider identifier of the customer call the agent leg was joining.</param>
    /// <param name="hangupCause">The cause the provider reported for the failed agent leg, when it reported one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a live call was settled; otherwise <see langword="false"/>.</returns>
    Task<bool> FailAsync(
        string providerName,
        string peerProviderCallId,
        Telephony.Models.HangupCause? hangupCause,
        CancellationToken cancellationToken = default);
}
