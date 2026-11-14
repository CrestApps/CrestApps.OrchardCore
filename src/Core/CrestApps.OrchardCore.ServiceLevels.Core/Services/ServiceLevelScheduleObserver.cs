using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Keeps the running timers right when the hours of their schedule change: the time used so far is counted on the old
/// hours before the change, and the due and warning times are worked out again on the new hours once it commits.
/// </summary>
public sealed class ServiceLevelScheduleObserver : IBusinessHoursScheduleObserver
{
    private readonly IServiceLevelPolicyManager _policies;
    private readonly IServiceLevelTimerStore _timers;
    private readonly ServiceLevelClock _timerClock;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelScheduleObserver"/> class.
    /// </summary>
    /// <param name="policies">The policy manager.</param>
    /// <param name="timers">The timer store.</param>
    /// <param name="timerClock">The service level clock.</param>
    /// <param name="clock">The clock.</param>
    public ServiceLevelScheduleObserver(
        IServiceLevelPolicyManager policies,
        IServiceLevelTimerStore timers,
        ServiceLevelClock timerClock,
        IClock clock)
    {
        _policies = policies;
        _timers = timers;
        _timerClock = timerClock;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task ChangingAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        foreach (var timer in await RunningTimersAsync(scheduleId, cancellationToken))
        {
            await _timerClock.SettleAsync(timer, now, cancellationToken);
            await _timers.UpdateAsync(timer, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public Task ChangedAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        // The new hours are readable once the change commits.
        if (ShellScope.Current is null)
        {
            return Task.CompletedTask;
        }

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<ServiceLevelScheduleObserver>().RescheduleAsync(scheduleId));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Works out the due and warning times of the running timers on a schedule again, from now, on its current hours.
    /// </summary>
    /// <param name="scheduleId">The schedule.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many timers were rescheduled.</returns>
    public async Task<int> RescheduleAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var timers = await RunningTimersAsync(scheduleId, cancellationToken);

        foreach (var timer in timers)
        {
            await _timerClock.SettleAsync(timer, now, cancellationToken);
            await _timerClock.ScheduleAsync(timer, now, cancellationToken);
            await _timers.UpdateAsync(timer, cancellationToken);
        }

        return timers.Count;
    }

    private async Task<IReadOnlyList<ServiceLevelTimer>> RunningTimersAsync(string scheduleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(scheduleId))
        {
            return [];
        }

        var policyIds = (await _policies.GetAllAsync(cancellationToken))
            .Where(policy => string.Equals(policy.HoursScheduleId, scheduleId, StringComparison.Ordinal))
            .Select(policy => policy.ItemId)
            .ToArray();

        var timers = await _timers.ListRunningByPoliciesAsync(policyIds, cancellationToken);

        // A timer keeps the schedule it started on until its policy is retargeted.
        return [.. timers.Where(timer => string.Equals(timer.HoursScheduleId, scheduleId, StringComparison.Ordinal))];
    }
}
