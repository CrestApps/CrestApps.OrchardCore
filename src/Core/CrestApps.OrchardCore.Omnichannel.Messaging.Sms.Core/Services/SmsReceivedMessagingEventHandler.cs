using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// Feeds every inbound text into the messaging workspace. Each SMS provider's webhook raises
/// <see cref="OmnichannelConstants.Events.SmsReceived"/> on the shared Omnichannel event bus, so listening there
/// is what lets Twilio, Telnyx and any later provider reach the workspace without knowing it exists.
/// </summary>
public sealed class SmsReceivedMessagingEventHandler : IOmnichannelEventHandler
{
    private readonly IMessagingInboundProcessor _inboundProcessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsReceivedMessagingEventHandler"/> class.
    /// </summary>
    /// <param name="inboundProcessor">The workspace's inbound pipeline.</param>
    public SmsReceivedMessagingEventHandler(IMessagingInboundProcessor inboundProcessor)
    {
        _inboundProcessor = inboundProcessor;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
    {
        if (omnichannelEvent?.Message is null ||
            omnichannelEvent.EventType != OmnichannelConstants.Events.SmsReceived ||
            !string.Equals(omnichannelEvent.Message.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase) ||
            !omnichannelEvent.Message.IsInbound)
        {
            return;
        }

        await _inboundProcessor.ProcessAsync(omnichannelEvent.Message, cancellationToken);
    }
}
