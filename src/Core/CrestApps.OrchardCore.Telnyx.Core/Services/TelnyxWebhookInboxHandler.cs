using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Processes normalized Telnyx call-event payloads from the durable provider webhook inbox.
/// </summary>
public sealed class TelnyxWebhookInboxHandler : IProviderWebhookInboxHandler
{
    /// <summary>
    /// The stable technical name persisted with Telnyx call-event payloads.
    /// </summary>
    public const string HandlerTechnicalName = "telnyx-call-event";

    private readonly ITelnyxWebhookService _webhookService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelnyxWebhookInboxHandler"/> class.
    /// </summary>
    /// <param name="webhookService">The Telnyx webhook processing service.</param>
    public TelnyxWebhookInboxHandler(ITelnyxWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    /// <inheritdoc/>
    public string TechnicalName => HandlerTechnicalName;

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var callEvent = JsonSerializer.Deserialize<TelnyxCallEvent>(payload, TelnyxJsonSerializerOptions.Default)
            ?? throw new InvalidDataException("The Telnyx call-event payload could not be deserialized.");

        await _webhookService.ProcessAsync(callEvent, cancellationToken);
    }
}
