using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Auto-schedules a scenario: builds an <see cref="IShiftScheduleSolver"/> problem from the team's active members,
/// their availability, skills and hours, the templates the team may use, the coverage requirements, and the shifts
/// already in the scenario or on the members' calendars, then adds the proposed shifts to the scenario.
/// </summary>
internal sealed class ScheduleAutoScheduler
{
    private readonly ScheduleScenarioStore _scenarioStore;
    private readonly SchedulePeriodStore _periodStore;
    private readonly DraftShiftStore _draftStore;
    private readonly TeamStore _teamStore;
    private readonly WorkerStore _workerStore;
    private readonly CoverageRequirementStore _requirementStore;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly IShiftTemplateManager _templateManager;
    private readonly ISkillAssignmentService _skillAssignments;
    private readonly ICalendarQueryService _queryService;
    private readonly IEnumerable<IShiftScheduleSolver> _solvers;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly ShiftPlanningOptions _options;
    private readonly IWorkerScheduleLimitsProvider _limits;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ScheduleAutoScheduler(
        ScheduleScenarioStore scenarioStore,
        SchedulePeriodStore periodStore,
        DraftShiftStore draftStore,
        TeamStore teamStore,
        WorkerStore workerStore,
        CoverageRequirementStore requirementStore,
        ActivityCodeStore activityCodeStore,
        IShiftTemplateManager templateManager,
        ISkillAssignmentService skillAssignments,
        ICalendarQueryService queryService,
        IEnumerable<IShiftScheduleSolver> solvers,
        IWorkerScheduleLimitsProvider limits,
        TeamTimeZoneResolver timeZones,
        IOptions<ShiftPlanningOptions> options,
        IClock clock,
        IStringLocalizer<ScheduleAutoScheduler> stringLocalizer)
    {
        _scenarioStore = scenarioStore;
        _periodStore = periodStore;
        _draftStore = draftStore;
        _teamStore = teamStore;
        _workerStore = workerStore;
        _requirementStore = requirementStore;
        _activityCodeStore = activityCodeStore;
        _templateManager = templateManager;
        _skillAssignments = skillAssignments;
        _queryService = queryService;
        _solvers = solvers;
        _limits = limits;
        _timeZones = timeZones;
        _options = options.Value;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<AutoScheduleResult> RunAsync(string scenarioId, AutoScheduleOptions options, string actor, CancellationToken cancellationToken = default)
    {
        options ??= new AutoScheduleOptions();

        var result = new AutoScheduleResult();
        var scenario = string.IsNullOrEmpty(scenarioId) ? null : await _scenarioStore.FindByIdAsync(scenarioId, cancellationToken);
        var period = scenario is null ? null : await _periodStore.FindByIdAsync(scenario.PeriodId, cancellationToken);
        var team = period is null ? null : await _teamStore.FindByIdAsync(period.TeamId, cancellationToken);

        if (team is null)
        {
            result.Errors.Add(S["The schedule doesn't exist."]);

            return result;
        }

        if (period.Status == SchedulePeriodStatus.Locked)
        {
            result.Errors.Add(S["The schedule is locked."]);

            return result;
        }

        var solverName = string.IsNullOrEmpty(options.Solver) ? _options.DefaultSolver : options.Solver;
        var solver = _solvers.FirstOrDefault(solver => string.Equals(solver.Name, solverName, StringComparison.OrdinalIgnoreCase)) ?? _solvers.FirstOrDefault();

        if (solver is null)
        {
            result.Errors.Add(S["No schedule solver is available."]);

            return result;
        }

        var timeZoneId = await _timeZones.GetAsync(team.ItemId, cancellationToken);
        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var templates = (await _templateManager.GetAllAsync(cancellationToken))
            .Where(template => template.Enabled && (template.TeamIds.Count == 0 || template.TeamIds.Contains(team.ItemId)))
            .ToDictionary(template => template.ItemId, StringComparer.Ordinal);
        var drafts = await _draftStore.ListForScenarioAsync(scenario.ItemId, cancellationToken);
        var members = (await _workerStore.ListByTeamInRangeAsync(team.ItemId, WorkforceDates.ToKey(period.StartDate), WorkforceDates.ToKey(period.EndDate), cancellationToken))
            .Where(worker => worker.Status == WorkerStatus.Active)
            .ToDictionary(worker => worker.ItemId, StringComparer.Ordinal);
        var interval = options.IntervalMinutes is >= 5 and <= 240 && 1440 % options.IntervalMinutes == 0 ? options.IntervalMinutes : 30;
        var problem = new ShiftScheduleProblem
        {
            Days = period.EndDate.DayNumber - period.StartDate.DayNumber + 1,
            IntervalMinutes = interval,
            MinRestMinutes = Math.Max(0, _options.MinRestHours) * 60,
            MaxConsecutiveDays = Math.Max(1, _options.MaxConsecutiveDays),
            TimeLimit = TimeSpan.FromSeconds(Math.Clamp(options.TimeLimitSeconds ?? _options.SolverTimeLimitSeconds, 1, 300)),
            Seed = options.Seed,
        };

        foreach (var template in templates.Values)
        {
            problem.Templates.Add(new ShiftScheduleTemplate
            {
                Id = template.ItemId,
                StartMinute = (template.Start.Hour * 60) + template.Start.Minute,
                LengthMinutes = template.LengthMinutes,
                PaidMinutes = template.LengthMinutes - template.Segments.Where(segment => IsUnpaid(segment.ActivityCodeId, codes)).Sum(segment => segment.LengthMinutes),
                Away = template.Segments
                    .Where(segment => IsAway(segment.ActivityCodeId, codes))
                    .Select(segment => new ShiftScheduleWindow(segment.OffsetMinutes, segment.OffsetMinutes + segment.LengthMinutes))
                    .ToList(),
                RequiredSkillIds = new HashSet<string>(template.RequiredSkillIds, StringComparer.Ordinal),
            });
        }

        foreach (var worker in members.Values)
        {
            problem.Workers.Add(await ToWorkerAsync(worker, team, period, problem.Days, cancellationToken));
        }

        var origin = period.StartDate.ToDateTime(TimeOnly.MinValue);

        foreach (var draft in drafts.Where(draft => draft.WorkerId is not null))
        {
            problem.FixedShifts.Add(new ShiftScheduleFixedShift
            {
                WorkerId = draft.WorkerId,
                Start = (int)(draft.Start - origin).TotalMinutes,
                End = (int)(draft.End - origin).TotalMinutes,
                PaidMinutes = ShiftEvents.PaidMinutes(draft.Start, draft.End, draft.Segments, codes),
                Away = draft.Segments
                    .Where(segment => IsAway(segment.ActivityCodeId, codes))
                    .Select(segment => new ShiftScheduleWindow((int)(segment.Start - origin).TotalMinutes, (int)(segment.End - origin).TotalMinutes))
                    .ToList(),
            });
        }

        foreach (var shift in await ListOtherShiftsAsync(team, period, members, timeZoneId, origin, codes, cancellationToken))
        {
            problem.FixedShifts.Add(shift);
        }

        foreach (var demand in await BuildDemandAsync(team, period, interval, cancellationToken))
        {
            problem.Demands.Add(demand);
        }

        var proposal = await solver.SolveAsync(problem, cancellationToken);
        var added = 0;

        foreach (var shift in proposal.Shifts)
        {
            if (!members.ContainsKey(shift.WorkerId) || !templates.TryGetValue(shift.TemplateId, out var template) || shift.Day < 0 || shift.Day >= problem.Days)
            {
                continue;
            }

            await _draftStore.CreateAsync(DefaultSchedulePlanningService.FromTemplate(scenario, period, shift.WorkerId, period.StartDate.AddDays(shift.Day), template, timeZoneId), cancellationToken);
            added++;
        }

        var run = new ScheduleSolverRun
        {
            Solver = proposal.Solver ?? solver.Name,
            CoverageScore = proposal.CoverageScore,
            MinimumScore = proposal.MinimumScore,
            ShiftsAdded = added,
            RuntimeMilliseconds = (long)proposal.Runtime.TotalMilliseconds,
            TimedOut = proposal.TimedOut,
            RunUtc = _clock.UtcNow,
            RunBy = actor,
        };

        scenario.LastRun = run;
        await _scenarioStore.UpdateAsync(scenario, cancellationToken);
        result.Run = run;

        return result;
    }

    private async Task<ShiftScheduleWorker> ToWorkerAsync(WorkerProfile worker, WorkforceTeam team, SchedulePeriod period, int days, CancellationToken cancellationToken)
    {
        var subject = SkillSubject.Worker(worker.ItemId);
        var first = (await _skillAssignments.GetEffectiveAsync(subject, period.StartDate, cancellationToken)).Select(assignment => assignment.SkillId);
        var last = (await _skillAssignments.GetEffectiveAsync(subject, period.EndDate, cancellationToken)).Select(assignment => assignment.SkillId);
        var limits = await _limits.GetAsync(worker, team.ItemId, cancellationToken);
        var maxHours = limits.MaxHoursPerWeek ?? worker.ContractedHoursPerWeek ?? _options.DefaultMaxHoursPerWeek;
        var result = new ShiftScheduleWorker
        {
            Id = worker.ItemId,

            // A skill counts only when it is held through the whole period.
            SkillIds = new HashSet<string>(first.Intersect(last, StringComparer.Ordinal), StringComparer.Ordinal),
            MaxMinutesPerWeek = (int)(maxHours * 60),
            MinMinutesPerWeek = (int)((worker.MinHoursPerWeek ?? 0) * 60),
            MaxMinutesPerDay = (int)((limits.MaxHoursPerDay ?? 0) * 60),
            MinRestMinutes = (int)(Math.Max(0, limits.MinRestHours) * 60),
            MaxConsecutiveDays = limits.MaxConsecutiveDays,
        };

        for (var day = 0; day < days; day++)
        {
            var date = period.StartDate.AddDays(day);

            if (!worker.MembershipsOn(date).Any(membership => membership.TeamId == team.ItemId)
                || (worker.HireDate.HasValue && worker.HireDate > date)
                || (worker.TerminationDate.HasValue && worker.TerminationDate < date))
            {
                continue;
            }

            var windows = worker.Availability.Where(window => window.Day == date.DayOfWeek).ToArray();

            if (windows.Any(window => window.Preference == AvailabilityPreference.Unavailable && window.Start == window.End))
            {
                continue;
            }

            result.Days.Add(day);

            foreach (var window in windows)
            {
                var start = (day * 1440) + (window.Start.Hour * 60) + window.Start.Minute;
                var end = (day * 1440) + (window.End.Hour * 60) + window.End.Minute;

                result.Windows.Add(new ShiftScheduleWindow(start, end <= start ? end + 1440 : end, window.Preference));
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the members' published shifts that aren't this schedule's: on this team from another schedule, which
    /// count, and on other teams, which only take the worker's time.
    /// </summary>
    private async Task<IReadOnlyList<ShiftScheduleFixedShift>> ListOtherShiftsAsync(WorkforceTeam team, SchedulePeriod period, IReadOnlyDictionary<string, WorkerProfile> members, string timeZoneId, DateTime origin, Dictionary<string, ActivityCode> codes, CancellationToken cancellationToken)
    {
        var teamIds = members.Values.SelectMany(worker => worker.TeamMemberships.Select(membership => membership.TeamId)).Append(team.ItemId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal);
        var calendarIds = (await _teamStore.GetAsync(teamIds, cancellationToken)).Select(item => item.CalendarId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray();

        if (calendarIds.Length == 0 || members.Count == 0)
        {
            return [];
        }

        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = calendarIds,
            FromUtc = CalendarTime.ToUtc(period.StartDate.AddDays(-1).ToDateTime(TimeOnly.MinValue), timeZoneId),
            ToUtc = CalendarTime.ToUtc(period.EndDate.AddDays(2).ToDateTime(TimeOnly.MinValue), timeZoneId),
            EventTypes = [WorkforceConstants.EventTypes.Shift],
            Sources = [WorkforceConstants.Source],
        }, cancellationToken);

        var shifts = new List<ShiftScheduleFixedShift>();

        foreach (var calendarEvent in occurrences.Select(occurrence => occurrence.Event).DistinctBy(calendarEvent => calendarEvent.ItemId))
        {
            if (!ShiftEvents.TryRead(calendarEvent, out var data) || data.ScheduleId == period.ItemId || !members.ContainsKey(data.WorkerId))
            {
                continue;
            }

            shifts.Add(new ShiftScheduleFixedShift
            {
                WorkerId = data.WorkerId,
                Start = Minutes(calendarEvent.StartUtc),
                End = Minutes(calendarEvent.EndUtc),
                PaidMinutes = ShiftEvents.PaidMinutes(calendarEvent, data, codes),
                Away = data.Segments
                    .Where(segment => IsAway(segment.ActivityCodeId, codes))
                    .Select(segment => new ShiftScheduleWindow(Minutes(segment.StartUtc), Minutes(segment.EndUtc)))
                    .ToList(),
                CountsTowardCoverage = data.TeamId == team.ItemId,
            });
        }

        return shifts;

        int Minutes(DateTime utc) => (int)(CalendarTime.ToLocal(utc, timeZoneId) - origin).TotalMinutes;
    }

    private async Task<IReadOnlyList<ShiftScheduleDemand>> BuildDemandAsync(WorkforceTeam team, SchedulePeriod period, int interval, CancellationToken cancellationToken)
    {
        var requirements = await _requirementStore.ListForTeamAsync(team.ItemId, cancellationToken);
        var perDay = 1440 / interval;
        var cells = new Dictionary<(string Skill, int Cell), (int Minimum, int Target, int? Maximum)>();

        for (var date = period.StartDate; date <= period.EndDate; date = date.AddDays(1))
        {
            var day = date.DayNumber - period.StartDate.DayNumber;

            foreach (var requirement in requirements.Where(requirement => CoverageRows.AppliesOn(requirement, date)))
            {
                foreach (var row in CoverageRows.RowsOn(requirement, date))
                {
                    var from = CoverageRows.StartMinute(row);
                    var to = CoverageRows.EndMinute(row);

                    for (var slot = (from + interval - 1) / interval; (slot + 1) * interval <= to && slot < perDay; slot++)
                    {
                        var key = (string.IsNullOrEmpty(requirement.SkillId) ? null : requirement.SkillId, (day * perDay) + slot);
                        var current = cells.TryGetValue(key, out var value) ? value : (Minimum: 0, Target: 0, Maximum: (int?)null);

                        cells[key] = (
                            current.Minimum + row.Minimum,
                            current.Target + row.Target,
                            row.Maximum.HasValue ? (current.Maximum ?? 0) + row.Maximum.Value : current.Maximum);
                    }
                }
            }
        }

        return cells.Select(cell => new ShiftScheduleDemand(cell.Key.Skill, cell.Key.Cell, cell.Value.Minimum, cell.Value.Target, cell.Value.Maximum)).ToArray();
    }

    private static bool IsUnpaid(string activityCodeId, Dictionary<string, ActivityCode> codes)
        => activityCodeId is not null && codes.TryGetValue(activityCodeId, out var code) && !code.IsPaid;

    private static bool IsAway(string activityCodeId, Dictionary<string, ActivityCode> codes)
        => activityCodeId is not null && codes.TryGetValue(activityCodeId, out var code) && !code.CountsTowardCoverage;
}
