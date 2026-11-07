using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Hands a queued <see cref="MessagingBroadcast"/> to the outreach engine, which fans it out to individual 1:1 threads,
/// one message per recipient.
/// </summary>
public interface IMessagingBroadcastService
{
    /// <summary>
    /// Processes one broadcast: starts the outreach run that sends it, in the caller's unit of work. A broadcast that
    /// already has a run is left as it is, and one without a channel, a sending address or a body fails.
    /// </summary>
    /// <param name="broadcast">The broadcast to process.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProcessAsync(MessagingBroadcast broadcast, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a run for every queued broadcast, and for every broadcast that was in progress before broadcasts were sent
    /// through the outreach engine.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ProcessPendingAsync(CancellationToken cancellationToken = default);
}
