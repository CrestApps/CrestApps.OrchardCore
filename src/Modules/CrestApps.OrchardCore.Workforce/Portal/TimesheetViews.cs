using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;

namespace CrestApps.OrchardCore.Workforce.Portal;

/// <summary>
/// Builds a timesheet's page, day by day, for the admin and the My Work portal.
/// </summary>
public sealed class TimesheetViews
{
    private readonly ITimesheetService _timesheets;
    private readonly ITimeClockService _timeClock;
    private readonly IAttendanceService _attendance;
    private readonly IPayCodeManager _payCodes;
    private readonly IActivityCodeManager _activityCodes;
    private readonly ITeamManager _teamManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimesheetViews"/> class.
    /// </summary>
    /// <param name="timesheets">The timesheets.</param>
    /// <param name="timeClock">The time clock.</param>
    /// <param name="attendance">The attendance service.</param>
    /// <param name="payCodes">The pay codes.</param>
    /// <param name="activityCodes">The activity codes.</param>
    /// <param name="teamManager">The team manager.</param>
    public TimesheetViews(ITimesheetService timesheets, ITimeClockService timeClock, IAttendanceService attendance, IPayCodeManager payCodes, IActivityCodeManager activityCodes, ITeamManager teamManager)
    {
        _timesheets = timesheets;
        _timeClock = timeClock;
        _attendance = attendance;
        _payCodes = payCodes;
        _activityCodes = activityCodes;
        _teamManager = teamManager;
    }

    /// <summary>
    /// Works a timesheet out again when it is open and builds its page.
    /// </summary>
    /// <param name="timesheet">The timesheet.</param>
    /// <param name="worker">Its worker.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    public async Task<TimesheetDetailsViewModel> BuildAsync(Timesheet timesheet, WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timesheet);
        ArgumentNullException.ThrowIfNull(worker);

        var (current, calculation) = await _timesheets.CalculateAsync(timesheet.ItemId, cancellationToken);

        timesheet = current ?? timesheet;

        var zone = calculation?.TimeZoneId ?? "UTC";
        var fromUtc = CalendarTime.ToUtc(timesheet.PeriodStart.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(timesheet.PeriodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var punches = await _timeClock.ListAsync(worker.ItemId, fromUtc, toUtc, cancellationToken);
        var exceptions = (await _attendance.PageAsync(new AttendanceQuery { WorkerId = worker.ItemId, From = timesheet.PeriodStart, To = timesheet.PeriodEnd }, 0, 500, cancellationToken)).Items;
        var codes = (await _payCodes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var activities = (await _activityCodes.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, code => code.Name, StringComparer.Ordinal);
        var team = string.IsNullOrEmpty(timesheet.TeamId) ? null : await _teamManager.FindByIdAsync(timesheet.TeamId, cancellationToken);
        var model = new TimesheetDetailsViewModel
        {
            Timesheet = timesheet,
            Worker = worker,
            TeamName = team?.Name,
            TimeZoneId = zone,
            PayCodes = codes,
            ActivityNames = activities,
            OnTheClockSinceUtc = calculation?.OnTheClockSinceUtc,
            Corrections = [.. await _attendance.ListPendingCorrectionsAsync(null, worker.ItemId, cancellationToken)],
        };

        for (var date = timesheet.PeriodStart; date <= timesheet.PeriodEnd; date = date.AddDays(1))
        {
            var day = date;

            model.Days.Add(new TimesheetDayViewModel
            {
                Date = day,
                Lines = timesheet.Lines.Where(line => line.Date == day).OrderBy(line => codes.GetValueOrDefault(line.PayCodeId ?? string.Empty)?.Kind).ToList(),
                Punches = punches.Where(punch => DateOnly.FromDateTime(CalendarTime.ToLocal(punch.Utc, zone)) == day).ToList(),
                Exceptions = exceptions.Where(exception => exception.Date == day).ToList(),
            });
        }

        model.Totals = timesheet.Lines
            .Where(line => !string.IsNullOrEmpty(line.PayCodeId))
            .GroupBy(line => line.PayCodeId, StringComparer.Ordinal)
            .Select(group => (Code: codes.GetValueOrDefault(group.Key), Hours: group.Sum(line => line.Hours)))
            .Where(total => total.Code is not null)
            .OrderBy(total => total.Code.Kind)
            .ThenBy(total => total.Code.Code, StringComparer.Ordinal)
            .ToList();

        return model;
    }
}
