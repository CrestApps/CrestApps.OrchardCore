using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Processes normalized provider voice events from the durable webhook inbox.
/// </summary>
public sealed class ProviderVoiceEventInboxHandler : IProviderWebhookInboxHandler
{
    /// <summary>
    /// The stable handler technical name persisted with normalized provider voice events.
    /// </summary>
    public const string HandlerTechnicalName = "provider-voice-event";

    private readonly IProviderVoiceEventService _providerVoiceEventService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderVoiceEventInboxHandler"/> class.
    /// </summary>
    /// <param name="providerVoiceEventService">The provider voice event service.</param>
    public ProviderVoiceEventInboxHandler(IProviderVoiceEventService providerVoiceEventService)
    {
        _providerVoiceEventService = providerVoiceEventService;
    }

    /// <inheritdoc/>
    public string TechnicalName => HandlerTechnicalName;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var providerEvent = JsonSerializer.Deserialize<ProviderVoiceEvent>(payload)
            ?? throw new InvalidDataException("The provider voice event payload could not be deserialized.");

        await _providerVoiceEventService.IngestAsync(providerEvent, cancellationToken);
    }
}
