using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IQueueWaitDeadlineEnforcer"/>.
/// </summary>
/// <remarks>
/// Acts on one caller at a time, never on the whole queue: two callers whose deadlines fall due together each run on
/// a scope of their own, and a queue-wide pass from each would both see the other's caller still waiting and act on
/// them twice. The queue-treatment sweep still makes its queue-wide pass as the backstop.
/// </remarks>
public sealed class QueueWaitDeadlineEnforcer : IQueueWaitDeadlineEnforcer
{
    private readonly IContactCenterDeadlineScheduler _scheduler;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityQueueService _queueService;
    private readonly IQueueLimitService _limitService;
    private readonly IDirectHoldTimeoutService _directHoldTimeoutService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueWaitDeadlineEnforcer"/> class.
    /// </summary>
    /// <param name="scheduler">The in-process deadline scheduler.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="interactionManager">The interaction manager, read for a held direct call's ring window.</param>
    /// <param name="queueService">The queue service that hands a caller on to an overflow queue.</param>
    /// <param name="limitService">The service that applies a queue's maximum-wait action.</param>
    /// <param name="directHoldTimeoutServices">The direct-hold timeout, present only when a voice feature is.</param>
    /// <param name="clock">The clock.</param>
    public QueueWaitDeadlineEnforcer(
        IContactCenterDeadlineScheduler scheduler,
        IQueueItemManager queueItemManager,
        IActivityQueueManager queueManager,
        IInteractionManager interactionManager,
        IActivityQueueService queueService,
        IQueueLimitService limitService,
        IEnumerable<IDirectHoldTimeoutService> directHoldTimeoutServices,
        IClock clock)
    {
        _scheduler = scheduler;
        _queueItemManager = queueItemManager;
        _queueManager = queueManager;
        _interactionManager = interactionManager;
        _queueService = queueService;
        _limitService = limitService;
        _directHoldTimeoutService = directHoldTimeoutServices.FirstOrDefault();
        _clock = clock;
    }

    /// <summary>
    /// The key a caller's wait deadline is held under.
    /// </summary>
    /// <param name="queueItemId">The queue item.</param>
    public static string GetDeadlineKey(string queueItemId) => $"queue-wait:{queueItemId}";

    /// <inheritdoc/>
    public async Task ArmAsync(string queueItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueItemId);

        var item = await _queueItemManager.FindByIdAsync(queueItemId, cancellationToken);
        var dueUtc = item?.Status == QueueItemStatus.Waiting
            ? await GetNextDueUtcAsync(item, queue: null, cancellationToken)
            : null;

        if (dueUtc is null)
        {
            _scheduler.Cancel(GetDeadlineKey(queueItemId));

            return;
        }

        _scheduler.Schedule(GetDeadlineKey(queueItemId), dueUtc.Value, CreateEnforcement(queueItemId));
    }

    /// <inheritdoc/>
    public async Task<DateTime?> EnforceAsync(string queueItemId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueItemId);

        var item = await _queueItemManager.FindByIdAsync(queueItemId, cancellationToken);

        // Ringing an agent, answered, or gone: whatever settles this caller next re-arms the deadline if it still
        // applies (a released offer returns them to the queue with their wait still running).
        if (item is null || item.Status != QueueItemStatus.Waiting)
        {
            return null;
        }

        if (ContactCenterConstants.IsDirectRoutingQueue(item.QueueId))
        {
            var holdDueUtc = await GetDirectHoldDueUtcAsync(item, cancellationToken);

            if (holdDueUtc is null)
            {
                return null;
            }

            if (holdDueUtc > _clock.UtcNow)
            {
                return holdDueUtc;
            }

            // The direct-hold timeout commits each call it settles and skips one another node already settled.
            await _directHoldTimeoutService.ProcessDueAsync(cancellationToken);

            return null;
        }

        var queue = ContactCenterConstants.IsCampaignQueue(item.QueueId)
            ? null
            : await _queueManager.FindByIdAsync(item.QueueId, cancellationToken);

        if (queue is null || !queue.Enabled)
        {
            return null;
        }

        var dueUtc = await GetNextDueUtcAsync(item, queue, cancellationToken);
        var now = _clock.UtcNow;

        if (dueUtc is null || dueUtc > now)
        {
            return dueUtc;
        }

        // The overflow hop first, as the sweep does, so a caller whose hop and maximum wait fall due together is
        // handed on rather than sent to voicemail. Handing them on publishes the move, which arms their deadline in
        // the next queue.
        if (OverflowScheduler.HasAnyTarget(queue))
        {
            if (item.QueueEnteredUtc == default)
            {
                item.QueueEnteredUtc = item.EnqueuedUtc;
            }

            var target = OverflowScheduler.SelectNextTarget(item, queue, now);

            if (!string.IsNullOrEmpty(target))
            {
                await _queueService.OverflowItemAsync(item, queue, target, cancellationToken);

                return null;
            }
        }

        await _limitService.EnforceMaxWaitAsync(item, queue, cancellationToken);

        return null;
    }

    /// <summary>
    /// Creates the work that enforces the caller's wait deadline when it falls due.
    /// </summary>
    /// <param name="queueItemId">The queue item.</param>
    internal static Func<IServiceProvider, CancellationToken, Task<DateTime?>> CreateEnforcement(string queueItemId)
    {
        return async (services, cancellationToken) =>
        {
            using var lease = services.GetRequiredService<IContactCenterFeatureWorkManager>().TryEnter(ContactCenterConstants.Feature.Queues);

            if (lease is null)
            {
                return null;
            }

            return await services.GetRequiredService<IQueueWaitDeadlineEnforcer>().EnforceAsync(queueItemId, cancellationToken);
        };
    }

    private async Task<DateTime?> GetNextDueUtcAsync(QueueItem item, ActivityQueue queue, CancellationToken cancellationToken)
    {
        if (ContactCenterConstants.IsDirectRoutingQueue(item.QueueId))
        {
            return await GetDirectHoldDueUtcAsync(item, cancellationToken);
        }

        // Campaign queues are virtual and carry no overflow or maximum wait.
        if (ContactCenterConstants.IsCampaignQueue(item.QueueId))
        {
            return null;
        }

        queue ??= await _queueManager.FindByIdAsync(item.QueueId, cancellationToken);

        if (queue is null || !queue.Enabled)
        {
            return null;
        }

        var overflowDueUtc = GetOverflowDueUtc(item, queue);
        var maxWaitDueUtc = QueueLimitService.GetMaxWaitDueUtc(item, queue);

        return overflowDueUtc is null
            ? maxWaitDueUtc
            : maxWaitDueUtc is null || overflowDueUtc < maxWaitDueUtc
                ? overflowDueUtc
                : maxWaitDueUtc;
    }

    private static DateTime? GetOverflowDueUtc(QueueItem item, ActivityQueue queue)
    {
        if (!OverflowScheduler.HasAnyTarget(queue))
        {
            return null;
        }

        var dueUtc = OverflowScheduler.GetNextDueUtc(item, queue);

        // An item written before it carried its own queue-entry time is timed from when it was enqueued, the way the
        // sweep times it.
        return dueUtc is DateTime due && item.QueueEnteredUtc == default
            ? item.EnqueuedUtc + (due - default(DateTime))
            : dueUtc;
    }

    private async Task<DateTime?> GetDirectHoldDueUtcAsync(QueueItem item, CancellationToken cancellationToken)
    {
        if (_directHoldTimeoutService is null)
        {
            return null;
        }

        var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);

        if (interaction is null)
        {
            return null;
        }

        var ringSeconds = ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;

        if (interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey, out var value) &&
            value is not null &&
            int.TryParse(value.ToString(), out var configured))
        {
            ringSeconds = configured;
        }

        // A ring window of zero means the entry point disabled voicemail: the call is held until the agent can take it.
        return ringSeconds > 0
            ? item.EnqueuedUtc.AddSeconds(ringSeconds)
            : null;
    }
}
