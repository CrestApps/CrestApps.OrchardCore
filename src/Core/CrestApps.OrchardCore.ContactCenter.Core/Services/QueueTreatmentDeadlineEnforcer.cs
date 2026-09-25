using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IQueueTreatmentDeadlineEnforcer"/>.
/// </summary>
/// <remarks>
/// Treatment used to be timed by the queue-treatment background task sweeping every ten seconds for most of each
/// minute, which held the tenant's background loop — and every other task queued behind it — for fifty seconds a run.
/// Each queue now holds one deadline for the soonest thing any of its callers is due, and the pass that deadline runs
/// returns the next one. A pass is queue-wide, as the sweep's was, because a caller's announced position depends on
/// everybody ahead of them; it runs under a per-queue lock and commits before letting go, so a pass on another scope
/// or node reads what this one played and cannot play it again.
/// </remarks>
public sealed class QueueTreatmentDeadlineEnforcer : IQueueTreatmentDeadlineEnforcer
{
    /// <summary>
    /// How soon to look again at a caller who was due something that could not be played — a call with no live leg
    /// yet, an announcement with nothing to say — so a caller who can never be treated does not spin a timer.
    /// </summary>
    internal static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How soon to try again when another pass holds the queue.
    /// </summary>
    internal static readonly TimeSpan LockedRetryInterval = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly IContactCenterDeadlineScheduler _scheduler;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IQueueTreatmentService _treatmentService;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueTreatmentDeadlineEnforcer"/> class.
    /// </summary>
    /// <param name="scheduler">The in-process deadline scheduler.</param>
    /// <param name="queueManager">The queue manager, read for the queue's treatment settings.</param>
    /// <param name="queueItemManager">The queue item manager, read for who is waiting.</param>
    /// <param name="treatmentService">The service that plays a queue's due treatment.</param>
    /// <param name="distributedLock">The lock that keeps two passes over one queue apart.</param>
    /// <param name="session">The session the pass commits before it lets go of the queue.</param>
    /// <param name="clock">The clock.</param>
    public QueueTreatmentDeadlineEnforcer(
        IContactCenterDeadlineScheduler scheduler,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IQueueTreatmentService treatmentService,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock)
    {
        _scheduler = scheduler;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _treatmentService = treatmentService;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
    }

    /// <summary>
    /// The key a queue's treatment deadline is held under.
    /// </summary>
    /// <param name="queueId">The queue.</param>
    public static string GetDeadlineKey(string queueId) => $"queue-treatment:{queueId}";

    /// <inheritdoc/>
    public async Task ArmAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        var queue = await FindTreatedQueueAsync(queueId, cancellationToken);
        var dueUtc = queue is null
            ? null
            : GetNextDueUtc(queue, await _queueItemManager.GetWaitingAsync(queueId, cancellationToken), _clock.UtcNow, afterPass: false);

        Arm(queueId, dueUtc);
    }

    /// <inheritdoc/>
    public async Task RunAndArmAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        Arm(queueId, await RunDueAsync(queueId, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<DateTime?> RunDueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        // Before the lock: the sweep asks this of every queue each minute, and most play nothing at all.
        var queue = await FindTreatedQueueAsync(queueId, cancellationToken);

        if (queue is null)
        {
            return null;
        }

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            $"ContactCenterQueueTreatment:{queueId}",
            _lockTimeout,
            _lockExpiration);

        if (!locked)
        {
            // Another pass is playing this queue's treatment; look again once it has committed what it played.
            return _clock.UtcNow + LockedRetryInterval;
        }

        await using var acquiredLock = locker;

        await _treatmentService.RunDueAsync(queue, cancellationToken);

        // Committed before the lock is let go, so the next pass reads what this one played.
        await _session.SaveChangesAsync(cancellationToken);

        var waiting = await _queueItemManager.GetWaitingAsync(queueId, cancellationToken);

        return GetNextDueUtc(queue, waiting, _clock.UtcNow, afterPass: true);
    }

    /// <summary>
    /// Creates the work that plays the queue's treatment when its deadline falls due.
    /// </summary>
    /// <param name="queueId">The queue.</param>
    internal static Func<IServiceProvider, CancellationToken, Task<DateTime?>> CreateWork(string queueId)
    {
        return async (services, cancellationToken) =>
        {
            // A feature that is being disabled drains its work; the sweep re-arms the queue if it comes back.
            using var lease = services.GetRequiredService<IContactCenterFeatureWorkManager>().TryEnter(ContactCenterConstants.Feature.Queues);

            if (lease is null)
            {
                return null;
            }

            return await services.GetRequiredService<IQueueTreatmentDeadlineEnforcer>().RunDueAsync(queueId, cancellationToken);
        };
    }

    /// <summary>
    /// The soonest time any waiting caller is next due something. Right after a pass, a caller still due now is one
    /// the pass could not treat, and is looked at again after <see cref="RetryInterval"/> rather than at once.
    /// </summary>
    internal static DateTime? GetNextDueUtc(ActivityQueue queue, IEnumerable<QueueItem> waiting, DateTime nowUtc, bool afterPass)
    {
        DateTime? next = null;

        foreach (var item in waiting)
        {
            if (QueueTreatmentPolicy.GetNextDueUtc(item, queue.Treatment) is not DateTime dueUtc)
            {
                continue;
            }

            if (afterPass && dueUtc <= nowUtc)
            {
                dueUtc = nowUtc + RetryInterval;
            }

            if (next is null || dueUtc < next)
            {
                next = dueUtc;
            }
        }

        return next;
    }

    private void Arm(string queueId, DateTime? dueUtc)
    {
        if (dueUtc is null)
        {
            _scheduler.Cancel(GetDeadlineKey(queueId));

            return;
        }

        _scheduler.Schedule(GetDeadlineKey(queueId), dueUtc.Value, CreateWork(queueId));
    }

    private async Task<ActivityQueue> FindTreatedQueueAsync(string queueId, CancellationToken cancellationToken)
    {
        // Direct-routing and campaign queues are virtual: nobody waiting in them is played queue treatment.
        if (ContactCenterConstants.IsDirectRoutingQueue(queueId) || ContactCenterConstants.IsCampaignQueue(queueId))
        {
            return null;
        }

        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

        // A disabled queue still has callers on hold in it, and they still hear it, as they did under the sweep.
        return queue?.Treatment is { } settings && QueueTreatmentPolicy.PlaysAnything(settings) ? queue : null;
    }
}
