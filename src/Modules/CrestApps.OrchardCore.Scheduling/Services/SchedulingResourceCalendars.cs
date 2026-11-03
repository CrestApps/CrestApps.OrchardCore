using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.Services;

/// <summary>
/// Gives a resource without a calendar its own Appointments calendar.
/// </summary>
public sealed class SchedulingResourceCalendars
{
    private readonly ICalendarService _calendarService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchedulingResourceCalendars"/> class.
    /// </summary>
    /// <param name="calendarService">The calendar service.</param>
    public SchedulingResourceCalendars(ICalendarService calendarService)
    {
        _calendarService = calendarService;
    }

    /// <summary>
    /// Creates an Appointments calendar for a resource when it has no calendar.
    /// </summary>
    /// <param name="resource">The resource.</param>
    /// <returns>The calendar's id, or <see langword="null"/> when it could not be made.</returns>
    public async Task<string> EnsureCalendarAsync(SchedulingResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (!string.IsNullOrEmpty(resource.CalendarId) && await _calendarService.FindByIdAsync(resource.CalendarId) is not null)
        {
            return resource.CalendarId;
        }

        var calendar = await _calendarService.NewAsync(SchedulingConstants.CalendarType);

        if (calendar is null)
        {
            return null;
        }

        calendar.Name = resource.Name;
        calendar.TimeZoneId = resource.TimeZoneId ?? calendar.TimeZoneId;
        calendar.Source = SchedulingConstants.EventSource;
        calendar.SourceId = resource.ItemId;

        var result = await _calendarService.CreateAsync(calendar, new CalendarChangeOptions { Actor = SchedulingConstants.EventSource });

        resource.CalendarId = result.Succeeded ? result.Value.ItemId : null;

        return resource.CalendarId;
    }
}
