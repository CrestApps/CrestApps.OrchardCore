using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.DialPad.Models;

namespace CrestApps.OrchardCore.DialPad.Services;

/// <summary>
/// Processes normalized DialPad call-event payloads from the durable provider webhook inbox.
/// </summary>
public sealed class DialPadWebhookInboxHandler : IProviderWebhookInboxHandler
{
    /// <summary>
    /// The stable technical name persisted with DialPad call-event payloads.
    /// </summary>
    public const string HandlerTechnicalName = "dialpad-call-event";

    private readonly IDialPadWebhookService _webhookService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialPadWebhookInboxHandler"/> class.
    /// </summary>
    /// <param name="webhookService">The DialPad webhook processing service.</param>
    public DialPadWebhookInboxHandler(IDialPadWebhookService webhookService)
    {
        _webhookService = webhookService;
    }

    /// <inheritdoc/>
    public string TechnicalName => HandlerTechnicalName;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var callEvent = JsonSerializer.Deserialize<DialPadCallEvent>(payload, DialPadJsonSerializerOptions.Default)
            ?? throw new InvalidDataException("The DialPad call-event payload could not be deserialized.");

        await _webhookService.ProcessAsync(callEvent, cancellationToken);
    }
}
