using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.DeliveryEvents;

/// <summary>
/// Processes a batch of delivery events committed to the durable provider inbox by the delivery events webhook. The
/// processor records each event under the provider's event identifier and skips one it has seen, so a replay of the
/// same batch (a redelivered webhook call, a retry after a crash) changes nothing twice.
/// </summary>
public sealed class EmailDeliveryEventsInboxHandler : IProviderWebhookInboxHandler
{
    private readonly IEmailDeliveryEventProcessor _processor;

    public EmailDeliveryEventsInboxHandler(IEmailDeliveryEventProcessor processor)
    {
        _processor = processor;
    }

    public string TechnicalName => EmailChannelConstants.DeliveryEventsInboxHandlerName;

    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var events = JsonSerializer.Deserialize<List<EmailDeliveryEvent>>(payload)
            ?? throw new InvalidDataException("The email delivery events payload could not be deserialized.");

        await _processor.ProcessAsync(events, cancellationToken);
    }
}
