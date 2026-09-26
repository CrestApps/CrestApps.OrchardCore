using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Bridges the Omnichannel automation's <see cref="IBusinessHoursGate"/> to the ContactCenter business-hours
/// calendars, so the automation can gate background-initiated sends on hours without referencing ContactCenter types.
/// </summary>
public sealed class BusinessHoursGate : IBusinessHoursGate
{
    private readonly IBusinessHoursService _businessHoursService;
    private readonly IBusinessHoursCalendarManager _calendarManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursGate"/> class.
    /// </summary>
    /// <param name="businessHoursService">The business-hours evaluation service.</param>
    /// <param name="calendarManager">The business-hours calendar manager.</param>
    public BusinessHoursGate(
        IBusinessHoursService businessHoursService,
        IBusinessHoursCalendarManager calendarManager)
    {
        _businessHoursService = businessHoursService;
        _calendarManager = calendarManager;
    }

    /// <inheritdoc/>
    public Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, string timeZoneId, CancellationToken cancellationToken = default)
        => _businessHoursService.IsOpenAsync(calendarId, utcInstant, timeZoneId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BusinessHoursCalendarOption>> GetCalendarOptionsAsync(CancellationToken cancellationToken = default)
    {
        var calendars = await _calendarManager.GetAllAsync(cancellationToken);

        return calendars
            .OrderBy(calendar => calendar.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(calendar => new BusinessHoursCalendarOption(calendar.ItemId, calendar.Name ?? calendar.ItemId))
            .ToList();
    }
}
