using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The time clock's <see cref="IWorkedTimeSource"/>: the time worked from punches, after the worker's rounding and
/// meals, in place of the published shifts.
/// </summary>
internal sealed class TimeClockWorkedTimeSource : IWorkedTimeSource
{
    private readonly ITimeClockService _timeClock;
    private readonly ITimesheetCalculator _calculator;
    private readonly WorkerTimeZones _timeZones;
    private readonly IClock _clock;

    public TimeClockWorkedTimeSource(ITimeClockService timeClock, ITimesheetCalculator calculator, WorkerTimeZones timeZones, IClock clock)
    {
        _timeClock = timeClock;
        _calculator = calculator;
        _timeZones = timeZones;
        _clock = clock;
    }

    public async Task<int> GetWorkedMinutesAsync(WorkerProfile worker, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        if (to < from)
        {
            return 0;
        }

        var zone = await _timeZones.GetAsync(worker, WorkerTimeZones.TeamOn(worker, from), cancellationToken);
        var policy = await _timeClock.GetPolicyAsync(worker, from, cancellationToken) ?? new TimeClockPolicy();
        var fromUtc = CalendarTime.ToUtc(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(to.AddDays(2).ToDateTime(TimeOnly.MinValue), zone);
        var punches = await _timeClock.ListAsync(worker.ItemId, fromUtc, toUtc, cancellationToken);

        if (punches.Count == 0)
        {
            return 0;
        }

        var calculation = _calculator.Calculate(new TimesheetCalculationInput
        {
            Policy = policy,
            TimeZoneId = zone,
            PeriodStart = from,
            PeriodEnd = to,
            Punches = punches,
            NowUtc = _clock.UtcNow,
        });

        return calculation.WorkedMinutes;
    }
}
