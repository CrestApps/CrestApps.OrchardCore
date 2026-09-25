using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingQueuePolicyReader"/> for a tenant without Work Distribution. There are no queues,
/// so no queue is found, and every caller takes its no-policy branch.
/// </summary>
public sealed class NullMessagingQueuePolicyReader : IMessagingQueuePolicyReader
{
    /// <inheritdoc/>
    public Task<MessagingQueuePolicy> ReadAsync(string queueId, CancellationToken cancellationToken = default)
        => Task.FromResult(MessagingQueuePolicy.NotFound);
}
