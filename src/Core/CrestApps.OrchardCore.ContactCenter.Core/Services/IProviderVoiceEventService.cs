using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Ingests normalized <see cref="ProviderVoiceEvent"/> instances from telephony providers and PBX
/// webhooks. It is the single seam through which provider call-state changes update the matching
/// interaction and call session, keeping orchestration and analytics in sync regardless of provider.
/// </summary>
public interface IProviderVoiceEventService
{
    /// <summary>
    /// Ingests a normalized provider voice event: matches it to the interaction and call session by
    /// provider call identifier, advances their normalized state and timestamps, bridges the agent for
    /// answered outbound calls on server-side ACD providers, and publishes the corresponding domain
    /// events. Events carrying an already-seen idempotency key are ignored.
    /// </summary>
    /// <param name="providerEvent">The normalized provider voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The updated or created call session, or <see langword="null"/> when no interaction matched.</returns>
    Task<CallSession> IngestAsync(ProviderVoiceEvent providerEvent, CancellationToken cancellationToken = default);
}
