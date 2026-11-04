using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Works out service level due times, around the clock or in a business hours calendar.
/// </summary>
public sealed class SlaClock
{
    // How far ahead the clock looks for open hours before giving up and counting around the clock.
    private static readonly TimeSpan _horizon = TimeSpan.FromDays(120);

    private readonly IEnumerable<IServiceHoursProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlaClock"/> class.
    /// </summary>
    /// <param name="providers">The service hours providers.</param>
    public SlaClock(IEnumerable<IServiceHoursProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>
    /// Gives when a number of minutes of the policy's hours, counted from a start, is up.
    /// </summary>
    /// <param name="startUtc">The start.</param>
    /// <param name="minutes">The minutes.</param>
    /// <param name="policy">The policy, or <see langword="null"/> for around the clock.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The due time.</returns>
    public async Task<DateTime> DueAsync(DateTime startUtc, int minutes, ServiceLevelPolicy policy, CancellationToken cancellationToken = default)
    {
        if (policy?.WeeklyHours is { Days.Count: > 0 } weekly && weekly.Closes > weekly.Opens)
        {
            var left = TimeSpan.FromMinutes(minutes);

            return Add(OpenWeekly(weekly, policy.TimeZoneId, startUtc, startUtc + _horizon), startUtc, ref left) ?? startUtc.AddMinutes(minutes);
        }

        if (string.IsNullOrEmpty(policy?.HoursCalendarId))
        {
            return startUtc.AddMinutes(minutes);
        }

        var remaining = TimeSpan.FromMinutes(minutes);
        var from = startUtc;

        while (from - startUtc < _horizon)
        {
            var to = from.AddDays(7);
            IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)> open = null;

            foreach (var provider in _providers)
            {
                open = await provider.GetOpenAsync(policy.HoursCalendarId, policy.TimeZoneId, from, to, cancellationToken);

                if (open is not null)
                {
                    break;
                }
            }

            if (open is null)
            {
                // No provider knows the calendar: count around the clock.
                return startUtc.AddMinutes(minutes);
            }

            if (Add(open, from, ref remaining) is { } due)
            {
                return due;
            }

            from = to;
        }

        return startUtc.AddMinutes(minutes);
    }

    /// <summary>
    /// Spends minutes in open stretches. Pure.
    /// </summary>
    /// <param name="open">The open stretches, in order.</param>
    /// <param name="fromUtc">Where counting starts.</param>
    /// <param name="remaining">The time left; what the stretches don't cover is left in it.</param>
    /// <returns>When the time ran out, or <see langword="null"/> when the stretches weren't enough.</returns>
    public static DateTime? Add(IEnumerable<(DateTime StartUtc, DateTime EndUtc)> open, DateTime fromUtc, ref TimeSpan remaining)
    {
        ArgumentNullException.ThrowIfNull(open);

        foreach (var (start, end) in open.OrderBy(window => window.StartUtc))
        {
            var begin = start < fromUtc ? fromUtc : start;

            if (end <= begin)
            {
                continue;
            }

            var length = end - begin;

            if (length >= remaining)
            {
                return begin + remaining;
            }

            remaining -= length;
        }

        return null;
    }

    /// <summary>
    /// Gives the open stretches of weekly hours, in a time zone, between two times. Pure.
    /// </summary>
    /// <param name="weekly">The hours.</param>
    /// <param name="timeZoneId">The time zone, or <see langword="null"/> for UTC.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <returns>The stretches, in order.</returns>
    public static IEnumerable<(DateTime StartUtc, DateTime EndUtc)> OpenWeekly(WeeklyHours weekly, string timeZoneId, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(weekly);

        var zone = TimeZoneInfo.Utc;

        if (!string.IsNullOrEmpty(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found))
        {
            zone = found;
        }

        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fromUtc, zone)).AddDays(-1);
        var lastDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(toUtc, zone));

        for (; day <= lastDay; day = day.AddDays(1))
        {
            if (!weekly.Days.Contains(day.DayOfWeek))
            {
                continue;
            }

            var start = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(weekly.Opens), zone);
            var end = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(weekly.Closes), zone);

            if (end > fromUtc && start < toUtc)
            {
                yield return (start, end);
            }
        }
    }

    /// <summary>
    /// Gives the target of a priority.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="priority">The priority.</param>
    /// <returns>The target, or <see langword="null"/>.</returns>
    public static ServiceLevelTarget TargetOf(ServiceLevelPolicy policy, WorkOrderPriority priority)
        => policy?.Targets.FirstOrDefault(target => target.Priority == priority);

    /// <summary>
    /// Gives when the warning before a due time is raised.
    /// </summary>
    /// <param name="startUtc">When the clock started.</param>
    /// <param name="dueUtc">When it is due.</param>
    /// <param name="warningPercent">How far through, in percent.</param>
    /// <returns>The warning time.</returns>
    public static DateTime WarningAt(DateTime startUtc, DateTime dueUtc, int warningPercent)
        => startUtc + TimeSpan.FromTicks((dueUtc - startUtc).Ticks * Math.Clamp(warningPercent, 1, 99) / 100);
}
