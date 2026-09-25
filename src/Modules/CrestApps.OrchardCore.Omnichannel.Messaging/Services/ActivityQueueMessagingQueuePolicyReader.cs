using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Reads the messaging queue policy from the real queue catalog. Registered only when Work Distribution is enabled,
/// so the workspace keeps working on a tenant that has no queues.
/// </summary>
public sealed class ActivityQueueMessagingQueuePolicyReader : IMessagingQueuePolicyReader
{
    private readonly IActivityQueueManager _queueManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueMessagingQueuePolicyReader"/> class.
    /// </summary>
    /// <param name="queueManager">The queue catalog.</param>
    public ActivityQueueMessagingQueuePolicyReader(IActivityQueueManager queueManager)
    {
        _queueManager = queueManager;
    }

    /// <inheritdoc/>
    public async Task<MessagingQueuePolicy> ReadAsync(string queueId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return MessagingQueuePolicy.NotFound;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

        return queue is null
            ? MessagingQueuePolicy.NotFound
            : new MessagingQueuePolicy(true, queue.FirstResponseTargetSeconds, queue.BusinessHoursCalendarId);
    }
}
