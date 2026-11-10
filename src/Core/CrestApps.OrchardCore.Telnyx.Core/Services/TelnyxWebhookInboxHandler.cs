using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Telnyx.Telemetry;
using CrestApps.OrchardCore.Webhooks;

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
    public WebhookHandlerReplaySafety ReplaySafety => WebhookHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var callEvent = JsonSerializer.Deserialize<TelnyxCallEvent>(payload, TelnyxJsonSerializerOptions.Default)
            ?? throw new InvalidDataException("The Telnyx call-event payload could not be deserialized.");

        // Linked to the span that received the delivery: its traceparent travels in the stored payload.
        await TelnyxTelemetry.ProcessWebhookAsync(_webhookService, callEvent, cancellationToken);
    }
}
