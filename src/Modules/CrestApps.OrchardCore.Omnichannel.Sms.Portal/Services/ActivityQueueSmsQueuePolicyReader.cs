using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Services;

/// <summary>
/// Reads the SMS queue policy from the real queue catalog. Registered only when Work Distribution is enabled,
/// so the portal keeps working on a tenant that has no queues.
/// </summary>
public sealed class ActivityQueueSmsQueuePolicyReader : ISmsQueuePolicyReader
{
    private readonly IActivityQueueManager _queueManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityQueueSmsQueuePolicyReader"/> class.
    /// </summary>
    /// <param name="queueManager">The queue catalog.</param>
    public ActivityQueueSmsQueuePolicyReader(IActivityQueueManager queueManager)
    {
        _queueManager = queueManager;
    }

    /// <inheritdoc/>
    public async Task<SmsQueuePolicy> ReadAsync(string queueId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return SmsQueuePolicy.NotFound;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

        return queue is null
            ? SmsQueuePolicy.NotFound
            : new SmsQueuePolicy(true, queue.FirstResponseTargetSeconds, queue.BusinessHoursCalendarId);
    }
}
