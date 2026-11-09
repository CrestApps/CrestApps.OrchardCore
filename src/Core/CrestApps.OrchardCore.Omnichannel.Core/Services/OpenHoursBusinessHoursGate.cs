using CrestApps.OrchardCore.BusinessHours.Services;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// The <see cref="IBusinessHoursGate"/> over the shared <see cref="IOpenHoursService"/>: a calendar any feature offers
/// (such as the Contact Center business hours) gates sends, and a tenant with no calendars, or a send that names none, is
/// always open, because gating on a calendar that does not exist would stop every send rather than the after-hours ones.
/// </summary>
public sealed class OpenHoursBusinessHoursGate : IBusinessHoursGate
{
    private readonly IOpenHoursService _openHoursService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenHoursBusinessHoursGate"/> class.
    /// </summary>
    /// <param name="openHoursService">The shared open-hours service.</param>
    public OpenHoursBusinessHoursGate(IOpenHoursService openHoursService)
    {
        _openHoursService = openHoursService;
    }

    /// <inheritdoc/>
    public async Task<bool> IsOpenAsync(string calendarId, DateTime utcInstant, string timeZoneId, CancellationToken cancellationToken = default)
        => await _openHoursService.IsOpenAsync(calendarId, utcInstant, timeZoneId, cancellationToken) ?? true;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BusinessHoursCalendarOption>> GetCalendarOptionsAsync(CancellationToken cancellationToken = default)
        => [.. (await _openHoursService.ListSchedulesAsync(cancellationToken)).Select(schedule => new BusinessHoursCalendarOption(schedule.Id, schedule.Name))];
}
