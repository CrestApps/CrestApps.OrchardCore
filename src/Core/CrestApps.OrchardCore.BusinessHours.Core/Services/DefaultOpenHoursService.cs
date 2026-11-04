using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Calendars;

namespace CrestApps.OrchardCore.BusinessHours.Core.Services;

/// <summary>
/// Answers from the registered <see cref="IHoursScheduleSource"/>s, in registration order: the first source that knows a
/// schedule answers for it. With no source, every schedule is unknown, so callers treat every moment as open.
/// </summary>
public sealed class DefaultOpenHoursService : IOpenHoursService
{
    private readonly IEnumerable<IHoursScheduleSource> _sources;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultOpenHoursService"/> class.
    /// </summary>
    /// <param name="sources">The schedule sources.</param>
    public DefaultOpenHoursService(IEnumerable<IHoursScheduleSource> sources)
    {
        _sources = sources;
    }

    /// <inheritdoc/>
    public async Task<bool?> IsOpenAsync(string scheduleId, DateTime utcInstant, string timeZoneId = null, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(scheduleId, cancellationToken);

        return schedule is null ? null : HoursScheduleEvaluator.IsOpen(schedule, utcInstant, timeZoneId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TimeWindow>> GetOpenWindowsAsync(string scheduleId, DateTime fromUtc, DateTime toUtc, string timeZoneId = null, CancellationToken cancellationToken = default)
    {
        var schedule = await FindAsync(scheduleId, cancellationToken);

        return schedule is null ? null : HoursScheduleEvaluator.GetOpenWindows(schedule, fromUtc, toUtc, timeZoneId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<HoursScheduleOption>> ListSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var schedules = new Dictionary<string, HoursScheduleOption>(StringComparer.Ordinal);

        foreach (var source in _sources)
        {
            foreach (var option in await source.ListAsync(cancellationToken) ?? [])
            {
                if (!string.IsNullOrEmpty(option?.Id))
                {
                    schedules.TryAdd(option.Id, option);
                }
            }
        }

        return [.. schedules.Values.OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    private async Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(scheduleId))
        {
            return null;
        }

        foreach (var source in _sources)
        {
            if (await source.FindAsync(scheduleId, cancellationToken) is { } schedule)
            {
                return schedule;
            }
        }

        return null;
    }
}
