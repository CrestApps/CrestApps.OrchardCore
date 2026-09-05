using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Reads the queue policy the SMS portal cares about. It exists so the portal can run without the Work
/// Distribution feature: a tenant with no queues resolves the null reader, every lookup reports "not found",
/// and the SLA and quiet-hours paths take their no-policy branch instead of failing to construct.
/// </summary>
public interface ISmsQueuePolicyReader
{
    /// <summary>
    /// Reads the policy for a queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SmsQueuePolicy> ReadAsync(string queueId, CancellationToken cancellationToken = default);
}
