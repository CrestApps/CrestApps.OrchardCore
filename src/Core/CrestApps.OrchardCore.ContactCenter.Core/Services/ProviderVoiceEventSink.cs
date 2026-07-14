using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Adapts provider-facing voice-event ingestion to the Contact Center call-session service.
/// </summary>
public sealed class ProviderVoiceEventSink : IProviderVoiceEventSink
{
    private readonly IProviderVoiceEventService _providerVoiceEventService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceEventSink"/> class.
    /// </summary>
    /// <param name="providerVoiceEventService">The Contact Center provider voice-event service.</param>
    public ProviderVoiceEventSink(IProviderVoiceEventService providerVoiceEventService)
    {
        _providerVoiceEventService = providerVoiceEventService;
    }

    /// <inheritdoc/>
    public async Task<bool> IngestAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);

        return await _providerVoiceEventService.IngestAsync(providerEvent, cancellationToken) is not null;
    }
}
