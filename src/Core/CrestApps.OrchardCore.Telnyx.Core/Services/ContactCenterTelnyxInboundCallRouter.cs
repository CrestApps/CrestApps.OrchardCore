using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Routes inbound Telnyx call events into the Contact Center voice front door, where entry points map the
/// dialed number (DID) to a queue or a specific agent.
/// </summary>
public sealed class ContactCenterTelnyxInboundCallRouter : ITelnyxInboundCallRouter
{
    private readonly IInboundVoiceEventSink _inboundVoiceEventSink;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTelnyxInboundCallRouter"/> class.
    /// </summary>
    /// <param name="inboundVoiceEventSink">The Contact Center inbound voice sink.</param>
    public ContactCenterTelnyxInboundCallRouter(IInboundVoiceEventSink inboundVoiceEventSink)
    {
        _inboundVoiceEventSink = inboundVoiceEventSink;
    }

    /// <inheritdoc/>
    public async Task<bool> RouteAsync(
        TelnyxCallEvent callEvent,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        await _inboundVoiceEventSink.RouteAsync(new InboundVoiceEvent
        {
            ProviderName = TelnyxConstants.ProviderTechnicalName,
            ProviderCallId = callEvent.CallControlId,
            FromAddress = callEvent.From,
            ToAddress = callEvent.To,
            ReceivedUtc = occurredUtc,
        }, cancellationToken);

        return true;
    }
}
