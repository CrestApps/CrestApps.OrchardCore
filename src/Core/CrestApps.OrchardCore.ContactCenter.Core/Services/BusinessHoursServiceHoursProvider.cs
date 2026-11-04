using CrestApps.OrchardCore.WorkOrders.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Gives a work-order service level policy's hours from a business hours calendar, so its clock counts only the
/// calendar's open hours and skips its holidays. The calendar is read in its own time zone; a missing or disabled
/// calendar is unknown, so the clock counts around the clock.
/// </summary>
public sealed class BusinessHoursServiceHoursProvider : IServiceHoursProvider
{
    private readonly IBusinessHoursCalendarManager _calendarManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursServiceHoursProvider"/> class.
    /// </summary>
    /// <param name="calendarManager">The business-hours calendar manager.</param>
    public BusinessHoursServiceHoursProvider(IBusinessHoursCalendarManager calendarManager)
    {
        _calendarManager = calendarManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<(DateTime StartUtc, DateTime EndUtc)>> GetOpenAsync(string calendarId, string timeZoneId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(calendarId))
        {
            return null;
        }

        var calendar = await _calendarManager.FindByIdAsync(calendarId, cancellationToken);

        if (calendar is null || !calendar.Enabled)
        {
            return null;
        }

        return DefaultBusinessHoursService.GetOpenWindows(calendar, fromUtc, toUtc, timeZoneId: null);
    }
}
