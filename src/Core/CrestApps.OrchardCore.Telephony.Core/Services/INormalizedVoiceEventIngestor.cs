using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Ingests normalized provider voice events through a single hardened path and fans them out to every
/// registered <see cref="INormalizedVoiceEventHandler"/>.
/// </summary>
public interface INormalizedVoiceEventIngestor
{
    /// <summary>
    /// Canonicalizes, gates, and fans out the specified normalized provider voice event.
    /// </summary>
    /// <param name="providerEvent">The normalized provider voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when at least one handler projected the event; otherwise, <see langword="false"/>.</returns>
    Task<bool> IngestAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken = default);
}
