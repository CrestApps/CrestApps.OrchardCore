namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Routes a parsed Telnyx inbound call when a higher-level voice feature can accept it.
/// </summary>
public interface ITelnyxInboundCallRouter
{
    /// <summary>
    /// Attempts to route the inbound call represented by the Telnyx webhook.
    /// </summary>
    /// <param name="callEvent">The parsed Telnyx call event.</param>
    /// <param name="occurredUtc">The provider event time in UTC.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the inbound call was routed; otherwise, <see langword="false"/>.</returns>
    Task<bool> RouteAsync(
        TelnyxCallEvent callEvent,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default);
}
