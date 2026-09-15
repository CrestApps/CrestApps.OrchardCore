namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Retries outbound messages the provider refused. The first attempt still runs inside the request so the agent
/// sees an immediate result; anything that fails is left queued with a scheduled retry, and this drains it.
/// </summary>
public interface ISmsOutboundOutbox
{
    /// <summary>
    /// Attempts every queued outbound message whose retry time has arrived.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages the provider accepted on this pass.</returns>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);
}
