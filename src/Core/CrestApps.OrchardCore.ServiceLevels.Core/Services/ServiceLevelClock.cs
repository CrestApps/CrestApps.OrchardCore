using CrestApps.OrchardCore.BusinessHours;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;

namespace CrestApps.OrchardCore.ServiceLevels.Core.Services;

/// <summary>
/// Counts a service level timer on its Business Hours schedule. The timer keeps the time it has used so far, in
/// seconds of its hours, so pausing, resuming and changing the target never count closed hours or paused time. A
/// schedule that is never open (changed so after the timer started) counts around the clock, the same for the time used
/// and the time left, so the timer neither freezes nor runs late forever, and the timer says so
/// (<see cref="ServiceLevelTimer.NoBusinessHours"/>).
/// </summary>
public sealed class ServiceLevelClock
{
    // How far ahead a schedule is looked at to tell it never opens.
    private static readonly TimeSpan _openHorizon = TimeSpan.FromDays(366);

    private readonly IOpenHoursService _openHours;
    private readonly Dictionary<string, bool> _neverOpen = new(StringComparer.Ordinal);

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

        var used = await _openHours.GetOpenTimeBetweenAsync(await CountedScheduleAsync(timer, timer.CountingSinceUtc, cancellationToken), timer.CountingSinceUtc, atUtc, cancellationToken: cancellationToken);

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

        var schedule = await CountedScheduleAsync(timer, fromUtc, cancellationToken);

        timer.CountingSinceUtc = fromUtc;
        timer.DueUtc = await _openHours.AddOpenTimeAsync(schedule, fromUtc, TimeSpan.FromSeconds(left), cancellationToken: cancellationToken);
        timer.WarnAtUtc = await _openHours.AddOpenTimeAsync(schedule, fromUtc, TimeSpan.FromSeconds(leftToWarning), cancellationToken: cancellationToken);
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

        return timer.UsedSeconds + ToSeconds(await _openHours.GetOpenTimeBetweenAsync(await CountedScheduleAsync(timer, timer.CountingSinceUtc, cancellationToken), timer.CountingSinceUtc, atUtc, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Counts the time a running timer used up to an instant on hours given as they were, such as a schedule's hours
    /// before they changed, and has it count on from that instant.
    /// </summary>
    /// <param name="timer">The timer.</param>
    /// <param name="hours">The hours, or <see langword="null"/> for around the clock.</param>
    /// <param name="atUtc">The instant.</param>
    public static void SettleOn(ServiceLevelTimer timer, HoursSchedule hours, DateTime atUtc)
    {
        ArgumentNullException.ThrowIfNull(timer);

        if (timer.State != ServiceLevelTimerState.Running || atUtc <= timer.CountingSinceUtc)
        {
            return;
        }

        var used = hours is null ? atUtc - timer.CountingSinceUtc : HoursScheduleEvaluator.GetOpenTime(hours, timer.CountingSinceUtc, atUtc);

        timer.UsedSeconds += ToSeconds(used);
        timer.CountingSinceUtc = atUtc;
    }

    /// <summary>
    /// Returns the open time between two instants on a schedule, in seconds.
    /// </summary>
    /// <param name="scheduleId">The schedule, or <see langword="null"/> for around the clock.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The open seconds.</returns>
    public async Task<long> GetOpenSecondsAsync(string scheduleId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => toUtc <= fromUtc ? 0 : ToSeconds(await _openHours.GetOpenTimeBetweenAsync(scheduleId, fromUtc, toUtc, cancellationToken: cancellationToken));

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

    /// <summary>
    /// Gets whether a schedule is never open in the year from an instant. A schedule nobody knows isn't "never open": it
    /// counts around the clock already.
    /// </summary>
    /// <param name="scheduleId">The schedule, or <see langword="null"/>.</param>
    /// <param name="fromUtc">The instant.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the schedule exists and has no open time in the year.</returns>
    public async Task<bool> IsNeverOpenAsync(string scheduleId, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scheduleId))
        {
            return false;
        }

        if (!_neverOpen.TryGetValue(scheduleId, out var neverOpen))
        {
            var windows = await _openHours.GetOpenWindowsAsync(scheduleId, fromUtc, fromUtc + _openHorizon, cancellationToken: cancellationToken);
            neverOpen = windows is { Count: 0 };
            _neverOpen[scheduleId] = neverOpen;
        }

        return neverOpen;
    }

    // The schedule the timer counts on: its own, or around the clock (null) while it is never open; the timer keeps which.
    private async Task<string> CountedScheduleAsync(ServiceLevelTimer timer, DateTime fromUtc, CancellationToken cancellationToken)
    {
        timer.NoBusinessHours = await IsNeverOpenAsync(timer.HoursScheduleId, fromUtc, cancellationToken);

        return timer.NoBusinessHours ? null : timer.HoursScheduleId;
    }

    private static long ToSeconds(TimeSpan time)
        => (long)Math.Round(time.TotalSeconds, MidpointRounding.AwayFromZero);
}
