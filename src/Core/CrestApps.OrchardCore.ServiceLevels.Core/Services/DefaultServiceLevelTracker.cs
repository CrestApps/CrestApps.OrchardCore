using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// The default <see cref="IServiceLevelTracker"/>.
/// </summary>
public sealed class DefaultServiceLevelTracker : IServiceLevelTracker
{
    private readonly IServiceLevelTimerStore _store;
    private readonly IServiceLevelPolicyLookup _policies;
    private readonly ServiceLevelClock _timerClock;
    private readonly IEnumerable<IServiceLevelObserver> _observers;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultServiceLevelTracker"/> class.
    /// </summary>
    /// <param name="store">The timer store.</param>
    /// <param name="policies">The policy lookup.</param>
    /// <param name="timerClock">Counts timers on their hours.</param>
    /// <param name="observers">The timer observers.</param>
    /// <param name="clock">The clock.</param>
    public DefaultServiceLevelTracker(
        IServiceLevelTimerStore store,
        IServiceLevelPolicyLookup policies,
        ServiceLevelClock timerClock,
        IEnumerable<IServiceLevelObserver> observers,
        IClock clock)
    {
        _store = store;
        _policies = policies;
        _timerClock = timerClock;
        _observers = observers;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<ServiceLevelTimer> StartAsync(ServiceLevelStart start, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(start);

        if (start.Subject.IsEmpty || string.IsNullOrEmpty(start.Metric))
        {
            return null;
        }

        var now = start.StartUtc ?? _clock.UtcNow;

        foreach (var previous in await ActiveAsync(start.Subject, start.Metric, cancellationToken))
        {
            Cancel(previous, now);
            await _store.UpdateAsync(previous, cancellationToken);
        }

        var policy = await _policies.FindAsync(start.PolicyId, cancellationToken);

        if (policy?.FindTarget(start.Metric, start.Priority) is not { } target)
        {
            return null;
        }

        var timer = new ServiceLevelTimer
        {
            Subject = start.Subject,
            Metric = start.Metric,
            Priority = start.Priority,
            StartedUtc = now,
            State = ServiceLevelTimerState.Running,
        };

        Apply(timer, policy, target);
        await _timerClock.ScheduleAsync(timer, now, cancellationToken);

        if (start.AlreadyWarned || start.AlreadyBreached)
        {
            timer.WarnedUtc = timer.WarnAtUtc;
        }

        if (start.AlreadyBreached)
        {
            timer.BreachedUtc = timer.DueUtc;
        }

        if (start.Paused)
        {
            timer.State = ServiceLevelTimerState.Paused;
            timer.PausedSinceUtc = now;
        }

        await _store.CreateAsync(timer, cancellationToken);

        return timer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelTimer>> PauseAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var paused = new List<ServiceLevelTimer>();

        foreach (var timer in await ActiveAsync(subject, metric, cancellationToken))
        {
            if (timer.State != ServiceLevelTimerState.Running)
            {
                continue;
            }

            // A timer that ran out before the monitor's next pass is breached, whatever happens to it now.
            var breachedNow = MarkBreachedIfOverdue(timer, now);

            await _timerClock.SettleAsync(timer, now, cancellationToken);

            timer.State = ServiceLevelTimerState.Paused;
            timer.PausedSinceUtc = now;

            await _store.UpdateAsync(timer, cancellationToken);
            paused.Add(timer);

            if (breachedNow)
            {
                await NotifyAsync(observer => observer.BreachedAsync(timer, cancellationToken));
            }
        }

        return paused;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelTimer>> ResumeAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var resumed = new List<ServiceLevelTimer>();

        foreach (var timer in await ActiveAsync(subject, metric, cancellationToken))
        {
            if (timer.State != ServiceLevelTimerState.Paused)
            {
                continue;
            }

            timer.State = ServiceLevelTimerState.Running;
            timer.PausedSinceUtc = null;

            await _timerClock.ScheduleAsync(timer, now, cancellationToken);
            await _store.UpdateAsync(timer, cancellationToken);
            resumed.Add(timer);
        }

        return resumed;
    }

    /// <inheritdoc/>
    public async Task<ServiceLevelTimer> CompleteAsync(RecordRef subject, string metric, DateTime? atUtc = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(metric))
        {
            return null;
        }

        var active = await ActiveAsync(subject, metric, cancellationToken);

        if (active.Count == 0)
        {
            return null;
        }

        // The newest cycle of the metric is the one being answered.
        var timer = active[0];

        var at = atUtc ?? _clock.UtcNow;

        if (timer.State == ServiceLevelTimerState.Running)
        {
            MarkBreachedIfOverdue(timer, at);
            await _timerClock.SettleAsync(timer, at, cancellationToken);
        }

        timer.ElapsedSeconds = timer.UsedSeconds;
        timer.State = ServiceLevelTimerState.Completed;
        timer.PausedSinceUtc = null;
        timer.EndedUtc = at;

        await _store.UpdateAsync(timer, cancellationToken);
        await NotifyAsync(observer => observer.CompletedAsync(timer, cancellationToken));

        return timer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelTimer>> CancelAsync(RecordRef subject, string metric = null, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var cancelled = new List<ServiceLevelTimer>();

        foreach (var timer in await ActiveAsync(subject, metric, cancellationToken))
        {
            Cancel(timer, now);
            await _store.UpdateAsync(timer, cancellationToken);
            cancelled.Add(timer);
        }

        return cancelled;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceLevelTimer>> RetargetAsync(RecordRef subject, string policyId, string priority, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var policy = await _policies.FindAsync(policyId, cancellationToken);
        var kept = new List<ServiceLevelTimer>();

        foreach (var timer in await ActiveAsync(subject, null, cancellationToken))
        {
            if (policy?.FindTarget(timer.Metric, priority) is not { } target)
            {
                Cancel(timer, now);
                await _store.UpdateAsync(timer, cancellationToken);

                continue;
            }

            // The time used so far is counted on the old hours, then the rest on the new target and hours.
            await _timerClock.SettleAsync(timer, now, cancellationToken);

            timer.Priority = priority;
            Apply(timer, policy, target);

            if (timer.State == ServiceLevelTimerState.Running)
            {
                await _timerClock.ScheduleAsync(timer, now, cancellationToken);
            }

            await _store.UpdateAsync(timer, cancellationToken);
            kept.Add(timer);
        }

        return kept;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ServiceLevelTimer>> GetTimersAsync(RecordRef subject, bool activeOnly = false, CancellationToken cancellationToken = default)
        => _store.ListBySubjectAsync(subject, activeOnly, cancellationToken);

    private async Task<IReadOnlyList<ServiceLevelTimer>> ActiveAsync(RecordRef subject, string metric, CancellationToken cancellationToken)
    {
        var timers = await _store.ListBySubjectAsync(subject, activeOnly: true, cancellationToken);

        return string.IsNullOrEmpty(metric)
            ? timers
            : [.. timers.Where(timer => string.Equals(timer.Metric, metric, StringComparison.OrdinalIgnoreCase))];
    }

    private static void Apply(ServiceLevelTimer timer, ServiceLevelPolicy policy, ServiceLevelTarget target)
    {
        timer.PolicyId = policy.ItemId;
        timer.TargetSeconds = target.Minutes * 60L;
        timer.HoursScheduleId = policy.HoursScheduleId;
        timer.WarningPercent = policy.WarningPercent;
    }

    private static void Cancel(ServiceLevelTimer timer, DateTime atUtc)
    {
        timer.State = ServiceLevelTimerState.Cancelled;
        timer.PausedSinceUtc = null;
        timer.EndedUtc = atUtc;
    }

    private static bool MarkBreachedIfOverdue(ServiceLevelTimer timer, DateTime atUtc)
    {
        if (timer.BreachedUtc is not null || atUtc <= timer.DueUtc)
        {
            return false;
        }

        timer.BreachedUtc = timer.DueUtc;

        return true;
    }

    private async Task NotifyAsync(Func<IServiceLevelObserver, Task> notify)
    {
        foreach (var observer in _observers)
        {
            await notify(observer);
        }
    }
}
