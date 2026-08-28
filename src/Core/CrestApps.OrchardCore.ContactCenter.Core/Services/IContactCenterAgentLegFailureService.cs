namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Settles a call whose agent leg failed after the platform had already originated it.
/// </summary>
/// <remarks>
/// A provider that delivers a call to a soft phone by originating a second leg reports that leg's failure on the
/// leg's own identifier, which belongs to no interaction, so the failure is discarded by normalization. Nothing
/// then ends the call: the customer stays connected to an agent who was never reached, the agent stays "on a
/// call" and is offered no further work, and recovery skips the record because recovery never touches an
/// unsettled interaction. This service is the path that failure takes instead.
/// </remarks>
public interface IContactCenterAgentLegFailureService
{
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
