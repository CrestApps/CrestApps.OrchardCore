using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Offers the Contact Center's business hours calendars to the shared <see cref="IOpenHoursService"/>, so queues, entry
/// points, automated sends and work-order service levels read the same hours. A missing or disabled calendar is unknown.
/// </summary>
public sealed class BusinessHoursCalendarScheduleSource : IHoursScheduleSource
{
    private readonly IBusinessHoursCalendarManager _calendarManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursCalendarScheduleSource"/> class.
    /// </summary>
    /// <param name="calendarManager">The business-hours calendar manager.</param>
    public BusinessHoursCalendarScheduleSource(IBusinessHoursCalendarManager calendarManager)
    {
        _calendarManager = calendarManager;
    }

    /// <inheritdoc/>
    public async Task<HoursSchedule> FindAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scheduleId))
        {
            return null;
        }

        var calendar = await _calendarManager.FindByIdAsync(scheduleId, cancellationToken);

        return calendar is null || !calendar.Enabled ? null : calendar.ToHoursSchedule();
    }

    /// <inheritdoc/>
    /// <remarks>Lists the enabled calendars by name.</remarks>
    public async Task<IReadOnlyList<HoursScheduleOption>> ListAsync(CancellationToken cancellationToken = default)
    {
        var calendars = await _calendarManager.GetAllAsync(cancellationToken);

        return [.. calendars
            .Where(calendar => calendar.Enabled)
            .OrderBy(calendar => calendar.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(calendar => new HoursScheduleOption(calendar.ItemId, calendar.Name ?? calendar.ItemId))];
    }
}
