using System.Globalization;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Finding a schedule in the user's teams, its draft shifts and their published shifts, the team's time zone, and the
/// coverage and labor rule summaries the shift schedule AI tools return, as the Shift Schedules pages show them.
/// </summary>
internal static class ShiftScheduleToolSupport
{
    /// <summary>
    /// The coverage interval the planner board uses.
    /// </summary>
    public const int CoverageIntervalMinutes = 30;

    /// <summary>
    /// The most draft shifts one result lists.
    /// </summary>
    public const int MaxShifts = 300;

    /// <summary>
    /// The most labor rule problems or changes one result lists.
    /// </summary>
    public const int MaxProblems = 50;

    /// <summary>
    /// Finds a schedule and its team, and checks the user manages the team, as the schedule pages do.
    /// </summary>
    public static async Task<(SchedulePeriod Period, Team Team, string Error)> FindPeriodAsync(IServiceProvider services, ClaimsPrincipal user, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return (null, null, "Give the schedule's ID (scheduleId), from listShiftSchedules.");
        }

        var period = await services.GetRequiredService<ISchedulePlanningService>().FindPeriodAsync(id.Trim(), cancellationToken);
        var team = period is null ? null : await services.GetRequiredService<ITeamManager>().FindByIdAsync(period.TeamId, cancellationToken);

        if (period is null || team is null)
        {
            return (null, null, $"No schedule has the ID '{id}'. Use listShiftSchedules to find it.");
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        return scope.Covers(team.ItemId)
            ? (period, team, null)
            : (null, null, $"The user doesn't manage {team.Name}, so its schedules can't be read or changed.");
    }

    /// <summary>
    /// Finds one team the user manages, by name or ID.
    /// </summary>
    public static async Task<(Team Team, string Error)> FindTeamAsync(IServiceProvider services, ClaimsPrincipal user, string teamName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            return (null, "Give the team (teamName), by name or ID.");
        }

        var team = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken))
            .Select(entry => entry.Team)
            .FirstOrDefault(item => item.ItemId == teamName || WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(teamName));

        if (team is null)
        {
            return (null, $"No team is called '{teamName}'.");
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        return scope.Covers(team.ItemId) ? (team, null) : (null, $"The user doesn't manage {team.Name}.");
    }

    /// <summary>
    /// Gets the team's time zone, or the site's, as the schedule pages show shifts.
    /// </summary>
    public static Task<string> TimeZoneAsync(IServiceProvider services, Team team)
        => PlanningOptions.TimeZoneOfAsync(team, services.GetRequiredService<ISiteService>());

    /// <summary>
    /// Finds a draft shift of a schedule.
    /// </summary>
    public static async Task<DraftShift> FindDraftAsync(IServiceProvider services, SchedulePeriod period, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return (await services.GetRequiredService<ISchedulePlanningService>().ListDraftShiftsAsync(period.PrimaryScenarioId, cancellationToken))
            .FirstOrDefault(shift => shift.ItemId == id.Trim());
    }

    /// <summary>
    /// Finds the shift on the published schedule that a draft shift became, by its shift key, as the shift page does.
    /// </summary>
    public static async Task<(CalendarEvent Event, ShiftData Data)?> FindPublishedAsync(IServiceProvider services, DraftShift draft, CancellationToken cancellationToken)
    {
        if (draft is null || string.IsNullOrEmpty(draft.ShiftKey))
        {
            return null;
        }

        var events = await services.GetRequiredService<ICalendarEventManager>().FindBySourceAsync(WorkforceConstants.Source, draft.ShiftKey, cancellationToken);

        foreach (var calendarEvent in events)
        {
            if (calendarEvent.Status != CalendarEventStatus.Cancelled && calendarEvent.TryGet<ShiftData>(out var data))
            {
                return (calendarEvent, data);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the names of workers by ID.
    /// </summary>
    public static async Task<Dictionary<string, string>> WorkerNamesAsync(IServiceProvider services, IEnumerable<string> workerIds, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IStaffMemberManager>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var workerId in workerIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            names[workerId] = (await manager.FindByIdAsync(workerId, cancellationToken))?.DisplayName ?? workerId;
        }

        return names;
    }

    /// <summary>
    /// Describes a draft shift: its day and wall-clock times in the team's time zone, the worker, template, breaks and notes.
    /// </summary>
    public static object Describe(DraftShift shift, IReadOnlyDictionary<string, string> workerNames, IReadOnlyDictionary<string, ShiftTemplate> templates, IReadOnlyCollection<ActivityCode> codes)
    {
        var template = shift.TemplateId is null ? null : templates.GetValueOrDefault(shift.TemplateId);

        return new
        {
            id = shift.ItemId,
            workerId = shift.WorkerId,
            worker = shift.WorkerId is null ? null : workerNames.GetValueOrDefault(shift.WorkerId, shift.WorkerId),
            open = shift.WorkerId is null,
            date = WorkforceAdminTools.Day(shift.Date),
            start = shift.Start.ToString("HH:mm", CultureInfo.InvariantCulture),
            end = shift.End.ToString("HH:mm", CultureInfo.InvariantCulture),
            endsNextDay = DateOnly.FromDateTime(shift.End) > shift.Date,
            template = template?.Name,
            templateId = shift.TemplateId,
            breaks = Breaks(shift, codes),
            notes = shift.Notes,
        };
    }

    /// <summary>
    /// Writes a shift's breaks and activities as the shift editor shows them, one per line, such as <c>13:00-13:30 Meal</c>.
    /// </summary>
    public static IReadOnlyList<string> Breaks(DraftShift shift, IReadOnlyCollection<ActivityCode> codes)
    {
        var text = SegmentFormat.Write(SegmentFormat.FromDraft(shift), codes, TimeOnly.FromDateTime(shift.Start));

        return string.IsNullOrEmpty(text)
            ? []
            : text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Describes labor rule problems, at most <see cref="MaxProblems"/>.
    /// </summary>
    public static IReadOnlyList<object> Problems(IEnumerable<LaborRuleViolation> violations)
    {
        return violations
            .Take(MaxProblems)
            .Select(violation => (object)new
            {
                rule = violation.RuleName ?? violation.RuleKey,
                ruleKey = violation.RuleKey,
                enforcement = violation.Enforcement.ToString(),
                workerId = violation.WorkerId,
                worker = violation.WorkerName,
                date = WorkforceAdminTools.Day(violation.Date),
                message = violation.Message,
            })
            .ToList();
    }

    /// <summary>
    /// Sums up, per day, where the schedule's shifts fall short of or under the team's coverage requirements, as the
    /// planner board does. Days without a requirement are left out.
    /// </summary>
    public static async Task<IReadOnlyList<object>> CoverageAsync(IServiceProvider services, SchedulePeriod period, Team team, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        if (to < from)
        {
            return [];
        }

        var timeZoneId = await TimeZoneAsync(services, team);
        var intervals = await services.GetRequiredService<ICoverageService>().ComputeAsync(new CoverageQuery
        {
            TeamId = team.ItemId,
            From = from,
            To = to,
            IntervalMinutes = CoverageIntervalMinutes,
            ScenarioId = period.PrimaryScenarioId,
        }, cancellationToken);

        var days = new SortedDictionary<DateOnly, DayCoverage>();

        foreach (var interval in intervals.Where(interval => interval.Target > 0 || interval.Minimum > 0))
        {
            var local = CalendarTime.ToLocal(interval.StartUtc, timeZoneId);
            var day = DateOnly.FromDateTime(local);

            if (day < from || day > to)
            {
                continue;
            }

            if (!days.TryGetValue(day, out var coverage))
            {
                days[day] = coverage = new DayCoverage();
            }

            if (interval.Scheduled < interval.Minimum)
            {
                coverage.ShortIntervals++;

                if (interval.Minimum - interval.Scheduled > coverage.WorstShort)
                {
                    coverage.WorstShort = interval.Minimum - interval.Scheduled;
                    coverage.WorstShortAt = TimeOnly.FromDateTime(local);
                }
            }

            if (interval.Gap > 0)
            {
                coverage.UnderTargetIntervals++;
            }

            if (interval.Gap > coverage.WorstGap)
            {
                coverage.WorstGap = interval.Gap;
                coverage.WorstGapAt = TimeOnly.FromDateTime(local);
            }
        }

        return days
            .Select(pair => (object)new
            {
                date = WorkforceAdminTools.Day(pair.Key),
                shortOfMinimum = pair.Value.ShortIntervals > 0,
                halfHoursShortOfMinimum = pair.Value.ShortIntervals,
                mostPeopleShort = pair.Value.WorstShort,
                mostShortAt = WorkforceAdminTools.Time(pair.Value.WorstShortAt),
                halfHoursUnderTarget = pair.Value.UnderTargetIntervals,
                mostPeopleUnderTarget = pair.Value.WorstGap,
                mostUnderTargetAt = WorkforceAdminTools.Time(pair.Value.WorstGapAt),
            })
            .ToList();
    }

    /// <summary>
    /// Gets the path of a schedule's planner board, or <see langword="null"/> when there is no request.
    /// </summary>
    public static string PlanPath(IServiceProvider services, string periodId)
        => WorkforceAdminTools.GetAdminPath(services, "Schedules", "Plan", new { id = periodId });

    private sealed class DayCoverage
    {
        public int ShortIntervals { get; set; }

        public int WorstShort { get; set; }

        public TimeOnly? WorstShortAt { get; set; }

        public int UnderTargetIntervals { get; set; }

        public int WorstGap { get; set; }

        public TimeOnly? WorstGapAt { get; set; }
    }
}
