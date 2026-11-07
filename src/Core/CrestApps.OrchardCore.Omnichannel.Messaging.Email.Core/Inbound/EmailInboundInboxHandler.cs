using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// Processes one inbound email from the durable provider webhook inbox. The email's <c>Message-ID</c> is the delivery key,
/// so a redelivered email is absorbed by the inbox instead of being stored and answered twice, and processing that fails
/// part-way is retried from storage rather than lost with the request that carried it.
/// </summary>
public sealed class EmailInboundInboxHandler : IProviderWebhookInboxHandler
{
    private readonly IEnumerable<IOmnichannelEventHandler> _eventHandlers;
    private readonly ISession _session;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailInboundInboxHandler"/> class.
    /// </summary>
    /// <param name="eventHandlers">The Omnichannel event handlers that observe an inbound email.</param>
    /// <param name="session">The session the inbound message is persisted with.</param>
    /// <param name="logger">The logger.</param>
    public EmailInboundInboxHandler(
        IEnumerable<IOmnichannelEventHandler> eventHandlers,
        ISession session,
        ILogger<EmailInboundInboxHandler> logger)
    {
        _eventHandlers = eventHandlers;
        _session = session;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string TechnicalName => EmailChannelConstants.InboxHandlerName;

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(payload);

        var message = JsonSerializer.Deserialize<OmnichannelMessage>(payload)
            ?? throw new InvalidDataException("The inbound email payload could not be deserialized.");

        await _session.SaveAsync(message, collection: OmnichannelConstants.CollectionName, cancellationToken: cancellationToken);

        var omnichannelEvent = new OmnichannelEvent
        {
            Id = message.ProviderMessageId,
            EventType = OmnichannelConstants.Events.EmailReceived,
            Subject = "Email received",
            Data = BinaryData.FromString(message.Content ?? string.Empty),
            Message = message,
        };

        await _eventHandlers.InvokeAsync((handler, evt) => handler.HandleAsync(evt), omnichannelEvent, _logger);
    }
}
