using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Projects the normalized provider voice event stream onto Contact Center interactions and call sessions.
/// <para>
/// The projection consumes the shared normalized stream instead of owning ingress. The ingestion lease is
/// already held by the provider-neutral ingestor when this runs, so the durable de-duplication record this
/// projection writes is the only one taken for the delivery.
/// </para>
/// </summary>
public sealed class ContactCenterVoiceProjection : INormalizedVoiceEventHandler
{
    private readonly IProviderVoiceEventSink _providerVoiceEventSink;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceProjection"/> class.
    /// </summary>
    /// <param name="providerVoiceEventSink">The Contact Center provider voice event sink.</param>
    /// <param name="logger">The logger instance.</param>
    public ContactCenterVoiceProjection(
        IProviderVoiceEventSink providerVoiceEventSink,
        ILogger<ContactCenterVoiceProjection> logger)
    {
        _providerVoiceEventSink = providerVoiceEventSink;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);

        var handled = await _providerVoiceEventSink.IngestAsync(providerEvent, cancellationToken);

        if (handled && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Normalized voice event for provider {ProviderName} call {CallId} flowed into Contact Center.",
                providerEvent.ProviderName,
                OperationalLogRedactor.Pseudonymize(providerEvent.ProviderCallId, OperationalLogIdentifierCategory.Call));
        }

        return handled;
    }
}
