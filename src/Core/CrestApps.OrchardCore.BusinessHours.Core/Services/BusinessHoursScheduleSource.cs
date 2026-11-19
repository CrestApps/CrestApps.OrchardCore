using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;

namespace CrestApps.OrchardCore.BusinessHours.Core.Services;

/// <summary>
/// Offers the schedules the Business Hours module keeps to the shared <see cref="IOpenHoursService"/>. A missing or
/// disabled schedule is unknown, so the records that use it count around the clock.
/// </summary>
public sealed class BusinessHoursScheduleSource : IHoursScheduleSource
{
    private readonly ICatalog<BusinessHoursSchedule> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursScheduleSource"/> class.
    /// </summary>
    /// <param name="store">The schedule store.</param>
    public BusinessHoursScheduleSource(ICatalog<BusinessHoursSchedule> store)
    {
        _store = store;
    }

    /// <inheritdoc/>
    public async Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scheduleId))
        {
            return null;
        }

        var schedule = await _store.FindByIdAsync(scheduleId, cancellationToken);

        return schedule is { Enabled: true }
            ? schedule.Hours ?? new HoursSchedule()
            : null;
    }

    /// <inheritdoc/>
    /// <remarks>Lists the enabled schedules by name.</remarks>
    public async Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await _store.GetAllAsync(cancellationToken);

        return [.. schedules
            .Where(schedule => schedule.Enabled)
            .OrderBy(schedule => schedule.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(schedule => new HoursScheduleOption(schedule.ItemId, schedule.Name ?? schedule.ItemId))];
    }
}
