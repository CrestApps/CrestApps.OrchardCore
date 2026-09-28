namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Routes a parsed Dialpad inbound call when a higher-level voice feature can accept it.
/// </summary>
public interface IDialpadInboundCallRouter
{
    /// <summary>
    /// Attempts to route the inbound call represented by the Dialpad webhook.
    /// </summary>
    /// <param name="callEvent">The parsed Dialpad call event.</param>
    /// <param name="occurredUtc">The provider event time in UTC.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// <see langword="true"/> when the inbound call was routed; otherwise, <see langword="false"/>.
    /// </returns>
    Task<bool> RouteAsync(
        DialpadCallEvent callEvent,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default);
}
