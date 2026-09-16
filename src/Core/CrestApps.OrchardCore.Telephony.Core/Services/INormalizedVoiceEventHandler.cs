using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Projects a normalized provider voice event onto one consumer's own state.
/// <para>
/// Every registered handler observes every ingested event. Handlers are peers, not a responsibility chain:
/// a handler that projects the event must not prevent another handler from projecting the same event,
/// because the telephony call history and the Contact Center call session are independent views of one
/// provider stream and both must stay in sync with it.
/// </para>
/// </summary>
public interface INormalizedVoiceEventHandler
{
    /// <summary>
    /// Gets the relative order in which the handler runs. Lower values run first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Projects the specified normalized voice event.
    /// </summary>
    /// <param name="providerEvent">
    /// The normalized voice event. Its provider name is already canonicalized, so handlers must not
    /// re-canonicalize it.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the handler projected the event; otherwise, <see langword="false"/>.</returns>
    Task<bool> HandleAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken = default);
}
