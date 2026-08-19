using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Routes inbound Dialpad call events into the Contact Center voice front door.
/// </summary>
public sealed class ContactCenterDialpadInboundCallRouter : IDialpadInboundCallRouter
{
    private readonly IInboundVoiceEventSink _inboundVoiceEventSink;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterDialpadInboundCallRouter"/> class.
    /// </summary>
    /// <param name="inboundVoiceEventSink">The Contact Center inbound voice sink.</param>
    public ContactCenterDialpadInboundCallRouter(IInboundVoiceEventSink inboundVoiceEventSink)
    {
        _inboundVoiceEventSink = inboundVoiceEventSink;
    }

    /// <inheritdoc/>
    public async Task<bool> RouteAsync(
        DialpadCallEvent callEvent,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        await _inboundVoiceEventSink.RouteAsync(new InboundVoiceEvent
        {
            ProviderName = DialpadConstants.ProviderTechnicalName,
            ProviderCallId = callEvent.CallId,
            FromAddress = callEvent.ExternalNumber,
            ToAddress = DialpadCallEventAddressResolver.ResolveServiceAddress(callEvent),
            CallerName = callEvent.ContactName,
            ReceivedUtc = occurredUtc,
        }, cancellationToken);

        return true;
    }
}
