using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for business-hours calendars.
/// </summary>
public interface IBusinessHoursCalendarManager : ICatalogManager<BusinessHoursCalendar>
{
    /// <summary>
    /// Finds the calendar with the specified unique name.
    /// </summary>
    /// <param name="name">The calendar name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching calendar, or <see langword="null"/> when none exists.</returns>
    Task<BusinessHoursCalendar> FindByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every enabled calendar.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The enabled calendars.</returns>
    Task<IReadOnlyCollection<BusinessHoursCalendar>> ListEnabledAsync(CancellationToken cancellationToken = default);
}
