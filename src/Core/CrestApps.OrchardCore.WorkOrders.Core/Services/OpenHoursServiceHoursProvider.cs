using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Gives a service level policy's hours from the shared business hours, so its clock counts only the open hours of the
/// calendar it names and skips that calendar's holidays, whichever module keeps the calendar. The calendar is read in its
/// own time zone; a calendar no source knows is unknown, so the clock counts around the clock.
/// </summary>
public sealed class OpenHoursServiceHoursProvider : IServiceHoursProvider
{
    private readonly IOpenHoursService _openHoursService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenHoursServiceHoursProvider"/> class.
    /// </summary>
    /// <param name="openHoursService">The shared open-hours service.</param>
    public OpenHoursServiceHoursProvider(IOpenHoursService openHoursService)
    {
        _openHoursService = openHoursService;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TimeWindow>> GetOpenAsync(string calendarId, string timeZoneId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => _openHoursService.GetOpenWindowsAsync(calendarId, fromUtc, toUtc, timeZoneId: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ServiceHoursCalendar>> ListCalendarsAsync(CancellationToken cancellationToken = default)
        => [.. (await _openHoursService.ListSchedulesAsync(cancellationToken)).Select(schedule => new ServiceHoursCalendar(schedule.Id, schedule.Name))];
}
