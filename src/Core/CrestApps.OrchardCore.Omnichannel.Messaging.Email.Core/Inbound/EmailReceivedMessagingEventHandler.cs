using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// Hands every inbound email raised on the Omnichannel event bus to the messaging workspace, which records it on the
/// customer's conversation and routes it, unless an automated (AI) conversation owns the customer, which answers it.
/// </summary>
public sealed class EmailReceivedMessagingEventHandler : IOmnichannelEventHandler
{
    private readonly IMessagingInboundProcessor _inboundProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailReceivedMessagingEventHandler"/> class.
    /// </summary>
    /// <param name="inboundProcessor">The workspace's inbound pipeline.</param>
    public EmailReceivedMessagingEventHandler(IMessagingInboundProcessor inboundProcessor)
    {
        _inboundProcessor = inboundProcessor;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (omnichannelEvent?.Message is null ||
            omnichannelEvent.EventType != OmnichannelConstants.Events.EmailReceived ||
            !string.Equals(omnichannelEvent.Message.Channel, OmnichannelConstants.Channels.Email, StringComparison.OrdinalIgnoreCase) ||
            !omnichannelEvent.Message.IsInbound)
        {
            return;
        }

        await _inboundProcessor.ProcessAsync(omnichannelEvent.Message, cancellationToken);
    }
}
