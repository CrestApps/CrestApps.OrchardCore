using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Telephony.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="INormalizedVoiceEventIngestor"/>.
/// <para>
/// This type owns the provider-neutral half of ingestion: it canonicalizes the provider identity, takes the
/// ingestion lease exactly once, and only then fans the event out. Every projection therefore observes the
/// same canonical identity and the same serialized ordering, and no projection takes a second lock on a
/// stream the ingestor already holds.
/// </para>
/// </summary>
public sealed class NormalizedVoiceEventIngestor : INormalizedVoiceEventIngestor
{
    private readonly IEnumerable<INormalizedVoiceEventHandler> _handlers;
    private readonly IProviderIdentityResolver _providerIdentityResolver;
    private readonly IVoiceIngressGate _ingressGate;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedVoiceEventIngestor"/> class.
    /// </summary>
    /// <param name="handlers">The projections that consume the normalized voice event stream.</param>
    /// <param name="providerIdentityResolver">The resolver used to canonicalize provider aliases before keying.</param>
    /// <param name="ingressGate">The gate that serializes each provider call stream.</param>
    /// <param name="logger">The logger instance.</param>
    public NormalizedVoiceEventIngestor(
        IEnumerable<INormalizedVoiceEventHandler> handlers,
        IProviderIdentityResolver providerIdentityResolver,
        IVoiceIngressGate ingressGate,
        ILogger<NormalizedVoiceEventIngestor> logger)
    {
        _handlers = handlers;
        _providerIdentityResolver = providerIdentityResolver;
        _ingressGate = ingressGate;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IngestAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);

        if (string.IsNullOrWhiteSpace(providerEvent.ProviderCallId))
        {
            return false;
        }

        // Canonicalize once, before the ingestion lock key is built, so that provider-contributed aliases
        // collapse onto a single stable identity for every downstream projection rather than being resolved
        // differently by each of them. The raw idempotency key is deliberately left untouched: scoping it is
        // the concern of the projection that owns the durable de-duplication record, and rewriting it here
        // would destroy the raw key that projection needs to recognize deliveries stored before scoping.
        providerEvent = providerEvent with
        {
            ProviderName = _providerIdentityResolver.Canonicalize(providerEvent.ProviderName),
        };

        await using var lease = await _ingressGate.AcquireAsync(
            providerEvent.ProviderName,
            providerEvent.ProviderCallId,
            cancellationToken);

        var handled = false;

        foreach (var handler in _handlers.OrderBy(handler => handler.Order))
        {
            // Every handler observes every event. A handler that claims the event does not consume it,
            // because the telephony call history and the Contact Center call session are independent
            // projections of the same stream and suppressing either one silently desynchronizes it.
            handled |= await handler.HandleAsync(providerEvent, cancellationToken);
        }

        if (!handled && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("No normalized voice event handler projected the ingested provider event.");
        }

        return handled;
    }
}
