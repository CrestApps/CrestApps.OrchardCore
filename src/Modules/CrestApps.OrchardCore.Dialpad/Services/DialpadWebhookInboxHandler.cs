using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Processes normalized Dialpad call-event payloads from the durable provider webhook inbox.
/// </summary>
public sealed class DialpadWebhookInboxHandler : IProviderWebhookInboxHandler
{
    /// <summary>
    /// The stable technical name persisted with Dialpad call-event payloads.
    /// </summary>
    public const string HandlerTechnicalName = "dialpad-call-event";

    private readonly IDialpadWebhookService _webhookService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialpadWebhookInboxHandler"/> class.
    /// </summary>
    /// <param name="webhookService">The Dialpad webhook processing service.</param>
    public DialpadWebhookInboxHandler(IDialpadWebhookService webhookService)
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

        var callEvent = JsonSerializer.Deserialize<DialpadCallEvent>(payload, DialpadJsonSerializerOptions.Default)
            ?? throw new InvalidDataException("The Dialpad call-event payload could not be deserialized.");

        await _webhookService.ProcessAsync(callEvent, cancellationToken);
    }
}
