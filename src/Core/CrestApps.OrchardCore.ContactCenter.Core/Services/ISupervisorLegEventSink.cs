using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Receives what a provider observes about the leg it rang to a supervisor's own phone for a monitor, whisper or barge
/// engagement.
/// </summary>
/// <remarks>
/// The supervisor's leg is a call the platform placed itself, so its events carry an identifier no interaction knows
/// and normal ingestion drops them. The provider reports them here, naming the customer call the leg was placed for,
/// and the Contact Center decides what they mean: the supervisor's engagement connecting or ending, or -- after a
/// takeover, when the supervisor's leg is what carries the call -- the call itself ending.
/// </remarks>
public interface ISupervisorLegEventSink
{
    /// <summary>
    /// Records that the supervisor's phone answered and the supervisor is now on the call.
    /// </summary>
    /// <param name="providerName">The provider.</param>
    /// <param name="providerCallId">The customer's call.</param>
    /// <param name="supervisorLegId">The supervisor's leg.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when a live engagement was found for the leg.</returns>
    Task<bool> OnAnsweredAsync(
        string providerName,
        string providerCallId,
        string supervisorLegId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decides what the supervisor's leg ending means: an engagement that ends, or, for a supervisor who took the call
    /// over, the end of the call.
    /// </summary>
    /// <param name="providerName">The provider.</param>
    /// <param name="providerCallId">The customer's call.</param>
    /// <param name="supervisorLegId">The supervisor's leg.</param>
    /// <param name="endedUtc">When the provider says the leg ended.</param>
    /// <param name="hangupCause">Why the leg ended, when the provider said.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the leg's end changed an engagement or the call.</returns>
    Task<bool> OnEndedAsync(
        string providerName,
        string providerCallId,
        string supervisorLegId,
        DateTime? endedUtc,
        HangupCause? hangupCause,
        CancellationToken cancellationToken = default);
}
