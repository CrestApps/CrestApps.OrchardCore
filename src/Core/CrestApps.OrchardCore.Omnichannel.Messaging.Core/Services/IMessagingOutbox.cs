namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Retries outbound messages the provider refused. The first attempt still runs inside the request so the agent
/// sees an immediate result; anything that fails is left queued with a scheduled retry, and this drains it.
/// </summary>
public interface IMessagingOutbox
{
    /// <summary>
    /// Attempts every queued outbound message whose retry time has arrived.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages the provider accepted on this pass.</returns>
    Task<int> DispatchDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Brings every message held for a later turn on one endpoint forward to now, so the next
    /// pass asks the channel for their turns again. Call it when the endpoint's sending limits change or its pause is
    /// lifted, so held mail no longer waits out turns given under the old limits.
    /// </summary>
    /// <param name="channel">The channel the endpoint sends on.</param>
    /// <param name="serviceAddress">The endpoint's address.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of messages brought forward.</returns>
    Task<int> BringForwardAsync(string channel, string serviceAddress, CancellationToken cancellationToken = default);
}
