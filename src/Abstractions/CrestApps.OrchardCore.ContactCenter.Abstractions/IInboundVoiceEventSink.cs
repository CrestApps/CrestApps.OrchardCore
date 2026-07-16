using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Routes normalized inbound provider voice events into Contact Center work.
/// </summary>
public interface IInboundVoiceEventSink
{
    /// <summary>
    /// Routes the specified inbound provider event.
    /// </summary>
    /// <param name="inboundEvent">The normalized inbound voice event.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task RouteAsync(
        InboundVoiceEvent inboundEvent,
        CancellationToken cancellationToken = default);
}
