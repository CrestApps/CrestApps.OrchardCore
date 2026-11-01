using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out again the timesheets of the workers who punched or had a shift lately, so missed punches and no-shows
/// are raised without anyone opening the timesheet.
/// </summary>
internal sealed class TimesheetRefresher
{
    private const int MaxWorkers = 2000;

    private readonly TimePunchStore _punchStore;
    private readonly TeamStore _teamStore;
    private readonly WorkerStore _workerStore;
    private readonly ICalendarQueryService _queryService;
    private readonly ITimesheetService _timesheets;
    private readonly WorkerTimeZones _timeZones;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public TimesheetRefresher(
        TimePunchStore punchStore,
        TeamStore teamStore,
        WorkerStore workerStore,
        ICalendarQueryService queryService,
        ITimesheetService timesheets,
        WorkerTimeZones timeZones,
        IClock clock,
        ILogger<TimesheetRefresher> logger)
    {
        _punchStore = punchStore;
        _teamStore = teamStore;
        _workerStore = workerStore;
        _queryService = queryService;
        _timesheets = timesheets;
        _timeZones = timeZones;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes the timesheets, returning how many workers it looked at.
    /// </summary>
    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var workerIds = new HashSet<string>(await _punchStore.ListWorkerIdsSinceAsync(now.AddHours(-36), cancellationToken), StringComparer.Ordinal);
        var calendarIds = (await _teamStore.ListAsync(cancellationToken))
            .Select(team => team.CalendarId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (calendarIds.Length > 0)
        {
            var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = calendarIds,
                FromUtc = now.AddHours(-36),
                ToUtc = now,
                EventTypes = [WorkforceConstants.EventTypes.Shift],
                Sources = [WorkforceConstants.Source],
            }, cancellationToken);

            foreach (var occurrence in occurrences)
            {
                if (ShiftEvents.TryRead(occurrence.Event, out var data) && !string.IsNullOrEmpty(data.WorkerId) && occurrence.Event.EndUtc <= now)
                {
                    workerIds.Add(data.WorkerId);
                }
            }
        }

        var count = 0;

        foreach (var chunk in workerIds.Take(MaxWorkers).Chunk(100))
        {
            foreach (var worker in await _workerStore.GetAsync(chunk, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var zone = await _timeZones.GetAsync(worker, WorkerTimeZones.TeamOn(worker, DateOnly.FromDateTime(now)), cancellationToken);
                var today = DateOnly.FromDateTime(CalendarTime.ToLocal(now, zone));

                foreach (var date in new[] { today.AddDays(-1), today })
                {
                    var result = await _timesheets.GetForDateAsync(worker, date, cancellationToken);

                    if (!result.Succeeded && _logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug("No timesheet for worker '{WorkerId}' on {Date}: {Reason}", worker.ItemId, date, string.Join(" ", result.Errors));
                    }
                }

                count++;
            }
        }

        return count;
    }
}
