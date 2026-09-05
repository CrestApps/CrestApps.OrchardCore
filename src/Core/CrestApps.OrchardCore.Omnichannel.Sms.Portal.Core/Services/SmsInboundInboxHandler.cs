using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Processes one inbound SMS from the durable provider webhook inbox. The provider's own message id is the
/// delivery key, so a redelivered text is absorbed by the inbox instead of being stored and answered twice, and
/// processing that fails part-way is retried from storage rather than lost with the request that carried it.
/// </summary>
public sealed class SmsInboundInboxHandler : IProviderWebhookInboxHandler
{
    /// <summary>
    /// The stable handler technical name persisted with normalized inbound SMS payloads.
    /// </summary>
    public const string HandlerTechnicalName = "sms-inbound";

    private readonly IEnumerable<IOmnichannelEventHandler> _eventHandlers;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsInboundInboxHandler"/> class.
    /// </summary>
    /// <param name="eventHandlers">The Omnichannel event handlers that observe an inbound SMS.</param>
    /// <param name="session">The session the inbound message is persisted with.</param>
    /// <param name="logger">The logger.</param>
    public SmsInboundInboxHandler(
        IEnumerable<IOmnichannelEventHandler> eventHandlers,
        ISession session,
        ILogger<SmsInboundInboxHandler> logger)
    {
        _eventHandlers = eventHandlers;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string TechnicalName => HandlerTechnicalName;

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var message = JsonSerializer.Deserialize<OmnichannelMessage>(payload)
            ?? throw new InvalidDataException("The inbound SMS payload could not be deserialized.");

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        var omnichannelEvent = new OmnichannelEvent
        {
            Id = message.ProviderMessageId,
            EventType = OmnichannelConstants.Events.SmsReceived,
            Subject = "SMS received",
            Data = BinaryData.FromString(message.Content ?? string.Empty),
            Message = message,
        };

        await _eventHandlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, _logger);
    }
}
