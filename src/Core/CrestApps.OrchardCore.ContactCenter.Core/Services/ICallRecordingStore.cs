using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Stores the call recordings catalog.
/// </summary>
public interface ICallRecordingStore : ICatalog<CallRecording>
{
    /// <summary>
    /// Finds the entry made for a provider's recording.
    /// </summary>
    /// <param name="providerRecordingId">The provider's identifier of the recording.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The entry, or <see langword="null"/> when there is none.</returns>
    Task<CallRecording> FindByProviderRecordingIdAsync(string providerRecordingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the newest entry listed for a recording that is still running on a call leg: one started by the platform
    /// that the provider has not reported saved yet.
    /// </summary>
    /// <param name="providerCallId">The provider's identifier of the recorded call leg.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The entry, or <see langword="null"/> when there is none.</returns>
    Task<CallRecording> FindRunningByProviderCallIdAsync(string providerCallId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the recordings made of a Contact Center interaction.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The recordings, oldest first.</returns>
    Task<IReadOnlyList<CallRecording>> ListByInteractionIdAsync(string interactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the playable recordings.
    /// </summary>
    /// <param name="query">The search.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>One page of recordings, newest first.</returns>
    Task<CallRecordingPage> QueryAsync(CallRecordingQuery query, CancellationToken cancellationToken = default);
}
