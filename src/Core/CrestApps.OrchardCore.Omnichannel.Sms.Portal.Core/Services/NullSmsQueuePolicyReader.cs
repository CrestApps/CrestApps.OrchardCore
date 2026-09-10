using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The default <see cref="ISmsQueuePolicyReader"/> for a tenant without Work Distribution. There are no queues,
/// so no queue is found, and every caller takes its no-policy branch.
/// </summary>
public sealed class NullSmsQueuePolicyReader : ISmsQueuePolicyReader
{
    /// <inheritdoc/>
    public Task<SmsQueuePolicy> ReadAsync(string queueId, CancellationToken cancellationToken = default)
        => Task.FromResult(SmsQueuePolicy.NotFound);
}
