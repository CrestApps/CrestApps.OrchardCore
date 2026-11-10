using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// One pass over the threads that missed their first-response target, timed by an in-process deadline between the
/// minute background runs.
/// </summary>
/// <remarks>
/// Cron cannot run a background task more often than once a minute, so the SLA task used to loop inside its run,
/// sweeping every thirty seconds, and held the tenant's background loop — and every task queued behind it — while it
/// waited. A run now makes one pass and holds a deadline for the next one: the soonest target still ahead, and no
/// later than the thirty seconds the loop used to keep. Each pass takes a lock and commits before letting it go, so
/// the background run and a deadline firing together cannot both announce the same breach.
/// </remarks>
internal sealed class MessagingFirstResponseSlaSweep
{
    /// <summary>
    /// The key the next pass is held under.
    /// </summary>
    internal const string DeadlineKey = "messaging-first-response-sla";

    /// <summary>
    /// The longest a pass waits for the next one: the cadence the background task's loop used to keep.
    /// </summary>
    internal static readonly TimeSpan MaximumInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How soon to try again when another pass holds the lock.
    /// </summary>
    internal static readonly TimeSpan LockedRetryInterval = TimeSpan.FromSeconds(1);

    private const string LockKey = "MessagingFirstResponseSlaSweep";

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly IMessagingFirstResponseSlaService _slaService;
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IContactCenterDeadlineScheduler _scheduler;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingFirstResponseSlaSweep"/> class.
    /// </summary>
    public MessagingFirstResponseSlaSweep(
        IMessagingFirstResponseSlaService slaService,
        IMessagingConversationStore conversationStore,
        IContactCenterDeadlineScheduler scheduler,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock)
    {
        _slaService = slaService;
        _conversationStore = conversationStore;
        _scheduler = scheduler;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
    }

    /// <summary>
    /// Makes one pass and holds the deadline for the next.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task RunAndArmAsync(CancellationToken cancellationToken = default)
    {
        var nextUtc = await RunAsync(cancellationToken);

        _scheduler.Schedule(DeadlineKey, nextUtc, CreateWork());
    }

    /// <summary>
    /// Announces every thread past its first-response target and commits the announcement.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>When the next pass is due.</returns>
    public async Task<DateTime> RunAsync(CancellationToken cancellationToken = default)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(LockKey, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return _clock.UtcNow + LockedRetryInterval;
        }

        await using var acquiredLock = locker;

        await _slaService.EscalateOverdueAsync(cancellationToken);

        // Committed before the lock is let go, so the next pass reads the breaches this one announced.
        await _session.SaveChangesAsync(cancellationToken);

        var nowUtc = _clock.UtcNow;
        var latestUtc = nowUtc + MaximumInterval;
        var nextDueUtc = await _conversationStore.GetNextFirstResponseDueUtcAsync(nowUtc, cancellationToken);

        return nextDueUtc is DateTime dueUtc && dueUtc < latestUtc ? dueUtc : latestUtc;
    }

    /// <summary>
    /// Creates the work the deadline runs.
    /// </summary>
    internal static Func<IServiceProvider, CancellationToken, Task<DateTime?>> CreateWork()
        => async (services, cancellationToken) => await services.GetRequiredService<MessagingFirstResponseSlaSweep>().RunAsync(cancellationToken);
}
