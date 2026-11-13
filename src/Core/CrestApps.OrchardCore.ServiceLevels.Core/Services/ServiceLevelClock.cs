using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Counts a service level timer on its Business Hours schedule. The timer keeps the time it has used so far, in
/// seconds of its hours, so pausing, resuming and changing the target never count closed hours or paused time.
/// </summary>
public sealed class ServiceLevelClock
{
    private readonly IOpenHoursService _openHours;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelClock"/> class.
    /// </summary>
    /// <param name="openHours">The open-hours service.</param>
    public ServiceLevelClock(IOpenHoursService openHours)
    {
        _openHours = openHours;
    }

    /// <summary>
    /// Adds the time a running timer has used since it was last settled to <see cref="ServiceLevelTimer.UsedSeconds"/>,
    /// and moves the point it counts from to the instant. A timer that isn't running is left alone.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="atUtc">The instant, in UTC.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    public async Task SettleAsync(ServiceLevelTimer timer, DateTime atUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timer);

        if (timer.State != ServiceLevelTimerState.Running || atUtc <= timer.CountingSinceUtc)
        {
            return;
        }

        var used = await _openHours.GetOpenTimeBetweenAsync(timer.HoursScheduleId, timer.CountingSinceUtc, atUtc, cancellationToken: cancellationToken);

        timer.UsedSeconds += ToSeconds(used);
        timer.CountingSinceUtc = atUtc;
    }

    /// <summary>
    /// Sets when a timer is due and when it warns, counting the time it has left from an instant.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="fromUtc">The instant the timer counts from, in UTC.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    public async Task ScheduleAsync(ServiceLevelTimer timer, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timer);

        var left = Math.Max(timer.TargetSeconds - timer.UsedSeconds, 0);
        var leftToWarning = Math.Max(WarningSeconds(timer) - timer.UsedSeconds, 0);

        timer.CountingSinceUtc = fromUtc;
        timer.DueUtc = await _openHours.AddOpenTimeAsync(timer.HoursScheduleId, fromUtc, TimeSpan.FromSeconds(left), cancellationToken: cancellationToken);
        timer.WarnAtUtc = await _openHours.AddOpenTimeAsync(timer.HoursScheduleId, fromUtc, TimeSpan.FromSeconds(leftToWarning), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Gets the time a timer has used as of an instant, in seconds of its hours.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="atUtc">The instant, in UTC.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The seconds used.</returns>
    public async Task<long> GetUsedSecondsAsync(ServiceLevelTimer timer, DateTime atUtc, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timer);

        if (timer.State != ServiceLevelTimerState.Running || atUtc <= timer.CountingSinceUtc)
        {
            return timer.ElapsedSeconds ?? timer.UsedSeconds;
        }

        return timer.UsedSeconds + ToSeconds(await _openHours.GetOpenTimeBetweenAsync(timer.HoursScheduleId, timer.CountingSinceUtc, atUtc, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Gets the seconds of its target after which a timer warns.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <returns>The seconds.</returns>
    public static long WarningSeconds(ServiceLevelTimer timer)
    {
        ArgumentNullException.ThrowIfNull(timer);

        var percent = timer.WarningPercent is >= 1 and <= 99 ? timer.WarningPercent : ServiceLevelsConstants.DefaultWarningPercent;

        return timer.TargetSeconds * percent / 100;
    }

    private static long ToSeconds(TimeSpan time)
        => (long)Math.Round(time.TotalSeconds, MidpointRounding.AwayFromZero);
}
