using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Receives what a provider observes about the leg it placed to a consult's destination.
/// </summary>
/// <remarks>
/// The consult leg is a call the platform placed itself, so its events carry an identifier no interaction knows and
/// normal ingestion drops them. The provider reports them here, naming the customer call and the consult it placed
/// the leg for, and the Contact Center decides what the event means for the transfer.
/// </remarks>
public interface IConsultLegEventSink
{
    /// <summary>
    /// Records that the destination answered, so the agent can complete the transfer.
    /// </summary>
    /// <param name="providerName">The provider.</param>
    /// <param name="providerCallId">The customer's call.</param>
    /// <param name="consultId">The consult the leg was placed for.</param>
    /// <param name="consultLegId">The consult leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a live consult was advanced.</returns>
    Task<bool> OnAnsweredAsync(
        string providerName,
        string providerCallId,
        string consultId,
        string consultLegId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides what the consult leg ending means: the destination walking away from a consult returns the caller to
    /// the agent, and the destination hanging up on a call it took over ends that call.
    /// </summary>
    /// <param name="providerName">The provider.</param>
    /// <param name="providerCallId">The customer's call.</param>
    /// <param name="consultId">The consult the leg was placed for.</param>
    /// <param name="consultLegId">The consult leg.</param>
    /// <param name="hangupCause">Why the leg ended, when the provider said.</param>
    /// <param name="endedUtc">When the leg ended, when the provider said.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>What the provider still has to do.</returns>
    Task<ConsultLegEndedOutcome> OnEndedAsync(
        string providerName,
        string providerCallId,
        string consultId,
        string consultLegId,
        HangupCause? hangupCause,
        DateTime? endedUtc,
        CancellationToken cancellationToken = default);
}
