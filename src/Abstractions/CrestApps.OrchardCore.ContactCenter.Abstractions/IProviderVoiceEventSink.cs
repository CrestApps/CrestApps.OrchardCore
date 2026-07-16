using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Ingests normalized provider voice events without exposing Contact Center persistence models.
/// </summary>
public interface IProviderVoiceEventSink
{
    /// <summary>
    /// Attempts to ingest a normalized provider voice event.
    /// </summary>
    /// <param name="providerEvent">The normalized provider voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a Contact Center call session handled the event; otherwise, <see langword="false"/>.</returns>
    Task<bool> IngestAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken = default);
}
