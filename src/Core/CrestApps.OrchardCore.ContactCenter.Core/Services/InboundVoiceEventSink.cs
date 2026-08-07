using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Adapts provider-facing inbound voice events to the Contact Center voice router.
/// </summary>
public sealed class InboundVoiceEventSink : IInboundVoiceEventSink
{
    private readonly IVoiceContactCenterCallRouter _voiceCallRouter;

    /// <summary>
    /// Initializes a new instance of the <see cref="InboundVoiceEventSink"/> class.
    /// </summary>
    /// <param name="voiceCallRouter">The Contact Center voice call router.</param>
    public InboundVoiceEventSink(IVoiceContactCenterCallRouter voiceCallRouter)
    {
        _voiceCallRouter = voiceCallRouter;
    }

    /// <inheritdoc/>
    public async Task<InboundVoiceRouteOutcome> RouteAsync(
        InboundVoiceEvent inboundEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inboundEvent);

        var result = await _voiceCallRouter.RouteInboundAsync(inboundEvent, cancellationToken);

        return new InboundVoiceRouteOutcome
        {
            InteractionId = result?.InteractionId,
        };
    }
}
