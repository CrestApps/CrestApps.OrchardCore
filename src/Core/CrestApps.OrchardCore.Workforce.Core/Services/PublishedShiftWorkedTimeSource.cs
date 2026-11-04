using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The built-in <see cref="IWorkedTimeSource"/>: the paid time of the worker's published shifts that start on the
/// days asked for.
/// </summary>
internal sealed class PublishedShiftWorkedTimeSource : IWorkedTimeSource
{
    private readonly WorkerShiftReader _shiftReader;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly WorkerTimeZones _timeZones;

    public PublishedShiftWorkedTimeSource(WorkerShiftReader shiftReader, ActivityCodeStore activityCodeStore, WorkerTimeZones timeZones)
    {
        _shiftReader = shiftReader;
        _activityCodeStore = activityCodeStore;
        _timeZones = timeZones;
    }

    public async Task<int> GetWorkedMinutesAsync(WorkerProfile worker, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        if (to < from)
        {
            return 0;
        }

        var zone = await _timeZones.GetAsync(worker, WorkerTimeZones.TeamOn(worker, from), cancellationToken);
        var shifts = await _shiftReader.ListAsync(worker, CalendarTime.ToUtc(from.ToDateTime(TimeOnly.MinValue), zone), CalendarTime.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone), cancellationToken);

        if (shifts.Count == 0)
        {
            return 0;
        }

        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var minutes = 0;

        foreach (var shift in shifts)
        {
            var day = DateOnly.FromDateTime(CalendarTime.ToLocal(shift.StartUtc, zone));

            if (day >= from && day <= to && ShiftEvents.TryRead(shift, out var data))
            {
                minutes += ShiftEvents.PaidMinutes(shift, data, codes);
            }
        }

        return minutes;
    }
}
