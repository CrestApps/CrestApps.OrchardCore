using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Reads the queue policy the messaging workspace cares about. It exists so the workspace can run without the Work
/// Distribution feature: a tenant with no queues resolves the null reader, every lookup reports "not found",
/// and the SLA and quiet-hours paths take their no-policy branch instead of failing to construct.
/// </summary>
public interface IMessagingQueuePolicyReader
{
    /// <summary>
    /// Reads the policy for a queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<MessagingQueuePolicy> ReadAsync(string queueId, CancellationToken cancellationToken = default);
}
