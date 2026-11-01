using System.Globalization;
using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// Adherence of the workers whose user is a contact center agent: their state now, their days, the alerts and the
/// exceptions.
/// </summary>
public sealed class AdherenceService
{
    private static readonly TimeSpan _scheduleWindow = TimeSpan.FromHours(14);

    private readonly ISession _session;
    private readonly AdherenceRuleSetManager _ruleSetManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;
    private readonly AgentWorkerService _agentWorkerService;
    private readonly ICalendarQueryService _calendarQueryService;
    private readonly IAgentStateHistory _stateHistory;
    private readonly IWorkforceEvents _events;
    private readonly IWorkerTimeZoneService _timeZones;
    private readonly IClock _clock;
    private List<AdherenceRuleSet> _ruleSets;
    private Dictionary<string, WorkforceTeam> _teams;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdherenceService"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="ruleSetManager">The rule set manager.</param>
    /// <param name="teamManager">The team manager.</param>
    /// <param name="workerManager">The worker manager.</param>
    /// <param name="agentWorkerService">The agent and worker link.</param>
    /// <param name="calendarQueryService">The calendar query service.</param>
    /// <param name="stateHistory">The agent state history.</param>
    /// <param name="events">The workforce events.</param>
    /// <param name="timeZones">The worker time zones.</param>
    /// <param name="clock">The clock.</param>
    public AdherenceService(
        ISession session,
        AdherenceRuleSetManager ruleSetManager,
        ITeamManager teamManager,
        IWorkerManager workerManager,
        AgentWorkerService agentWorkerService,
        ICalendarQueryService calendarQueryService,
        IAgentStateHistory stateHistory,
        IWorkforceEvents events,
        IWorkerTimeZoneService timeZones,
        IClock clock)
    {
        _session = session;
        _ruleSetManager = ruleSetManager;
        _teamManager = teamManager;
        _workerManager = workerManager;
        _agentWorkerService = agentWorkerService;
        _calendarQueryService = calendarQueryService;
        _stateHistory = stateHistory;
        _events = events;
        _timeZones = timeZones;
        _clock = clock;
    }

    /// <summary>
    /// Gets the rules a team follows: the rule set of the team or its nearest parent, or the default rules.
    /// </summary>
    /// <param name="teamId">The team, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rules.</returns>
    public async Task<IReadOnlyList<AdherenceRule>> RulesForTeamAsync(string teamId, CancellationToken cancellationToken = default)
    {
        _ruleSets ??= [.. await _ruleSetManager.GetAllAsync(cancellationToken)];

        var teamIds = new List<string>();

        if (!string.IsNullOrEmpty(teamId))
        {
            teamIds.Add(teamId);
            teamIds.AddRange((await _teamManager.GetAncestorsAsync(teamId, cancellationToken)).Select(team => team.ItemId));
        }

        foreach (var id in teamIds)
        {
            var set = _ruleSets.FirstOrDefault(candidate => candidate.TeamIds.Contains(id));

            if (set is not null)
            {
                return set.Rules;
            }
        }

        return AdherenceCalculator.DefaultRules();
    }

    /// <summary>
    /// Gets the current adherence of the agents among the given workers, with what they are scheduled for now.
    /// </summary>
    /// <param name="workers">The workers.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>One row per worker whose user is an agent.</returns>
    public async Task<IReadOnlyList<AdherenceBoardRow>> CurrentAsync(IEnumerable<WorkerProfile> workers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workers);

        var now = _clock.UtcNow;
        var linked = new List<(WorkerProfile Worker, AgentProfile Agent)>();

        foreach (var worker in workers.Where(worker => !string.IsNullOrEmpty(worker.UserId)))
        {
            var agent = await _agentWorkerService.FindAgentAsync(worker, cancellationToken);

            if (agent is not null)
            {
                linked.Add((worker, agent));
            }
        }

        if (linked.Count == 0)
        {
            return [];
        }

        var schedules = await SchedulesAsync(linked.Select(entry => entry.Worker.ItemId), now - _scheduleWindow, now + _scheduleWindow, cancellationToken);
        var teams = await TeamsAsync(cancellationToken);
        var rows = new List<AdherenceBoardRow>(linked.Count);

        foreach (var (worker, agent) in linked)
        {
            var teamId = TeamOf(worker);
            var rules = await RulesForTeamAsync(teamId, cancellationToken);
            var schedule = schedules.GetValueOrDefault(worker.ItemId) ?? [];
            var current = AdherenceCalculator.Current(schedule, agent.PresenceStatus, agent.PresenceReason, agent.PresenceChangedUtc, rules, now);
            var nextIndex = schedule.FindIndex(piece => piece.StartUtc > now);

            rows.Add(new AdherenceBoardRow
            {
                TimeZoneId = await TimeZoneOfAsync(worker, cancellationToken),
                WorkerId = worker.ItemId,
                WorkerName = worker.DisplayName,
                TeamId = teamId,
                TeamName = teamId is not null && teams.TryGetValue(teamId, out var team) ? team.Name : null,
                AgentId = agent.ItemId,
                Status = agent.PresenceStatus,
                Reason = agent.PresenceReason,
                StatusSinceUtc = agent.PresenceChangedUtc,
                Scheduled = current.Scheduled,
                Next = nextIndex >= 0 ? schedule[nextIndex] : null,
                InAdherence = current.InAdherence,
                OutSinceUtc = current.OutSinceUtc,
                Alert = current.Alert,
            });
        }

        return rows;
    }

    /// <summary>
    /// Gets the current adherence of the agents in the teams a user manages.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="teamId">The team to show, or <see langword="null"/> for every team the user manages.</param>
    /// <param name="scopeService">The scope service.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The rows, by team and name.</returns>
    public async Task<IReadOnlyList<AdherenceBoardRow>> BoardAsync(ClaimsPrincipal user, string teamId, IWorkforceScopeService scopeService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeService);

        var scope = await scopeService.GetManagedScopeAsync(user, cancellationToken);
        var teamIds = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(teamId))
        {
            if (!scope.All && !scope.TeamIds.Contains(teamId))
            {
                return [];
            }

            teamIds.UnionWith(await _teamManager.GetSubtreeIdsAsync(teamId, cancellationToken));
        }
        else if (scope.All)
        {
            teamIds.UnionWith((await TeamsAsync(cancellationToken)).Keys);
        }
        else
        {
            teamIds.UnionWith(scope.TeamIds);
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var workers = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);

        foreach (var id in teamIds)
        {
            foreach (var worker in await _workerManager.ListByTeamAsync(id, today, cancellationToken))
            {
                workers.TryAdd(worker.ItemId, worker);
            }
        }

        var rows = await CurrentAsync(workers.Values, cancellationToken);

        return [.. rows
            .OrderBy(row => row.InAdherence)
            .ThenBy(row => row.TeamName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(row => row.WorkerName, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <summary>
    /// Raises an alert for every agent out of adherence long enough, once per episode.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many alerts were raised.</returns>
    public async Task<int> RaiseAlertsAsync(CancellationToken cancellationToken = default)
    {
        var workers = new List<WorkerProfile>();

        foreach (var (agent, worker) in await _agentWorkerService.ListAsync(cancellationToken))
        {
            if (worker is not null && worker.Status == WorkerStatus.Active)
            {
                workers.Add(worker);
            }
        }

        var raised = 0;

        foreach (var row in await CurrentAsync(workers, cancellationToken))
        {
            if (!row.Alert || row.Scheduled is null || row.OutSinceUtc is null)
            {
                continue;
            }

            var key = string.Create(CultureInfo.InvariantCulture, $"{row.WorkerId}|{row.Scheduled.StartUtc:O}|{row.OutSinceUtc:O}");

            if (await _session.QueryIndex<AdherenceAlertIndex>(index => index.Key == key, collection: WorkforceConstants.CollectionName).FirstOrDefaultAsync(cancellationToken) is not null)
            {
                continue;
            }

            await _session.SaveAsync(new AdherenceAlert
            {
                ItemId = IdGenerator.GenerateId(),
                Key = key,
                WorkerId = row.WorkerId,
                RaisedUtc = _clock.UtcNow,
            }, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

            await _events.RaiseAsync(WorkforceEvents.OutOfAdherence, new Dictionary<string, object>
            {
                ["WorkerId"] = row.WorkerId,
                ["WorkerName"] = row.WorkerName,
                ["TeamId"] = row.TeamId,
                ["Status"] = row.Status.ToString(),
                ["Reason"] = row.Reason,
                ["ScheduledActivity"] = row.Scheduled.Kind.ToString(),
                ["OutSinceUtc"] = row.OutSinceUtc,
                ["Minutes"] = (int)(_clock.UtcNow - row.OutSinceUtc.Value).TotalMinutes,
            }, row.WorkerId, cancellationToken);

            raised++;
        }

        return raised;
    }

    /// <summary>
    /// Computes a worker's adherence over a day in their time zone.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The day, not saved; <see langword="null"/> when the worker's user is not an agent.</returns>
    public async Task<AdherenceDay> ComputeDayAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var agent = await _agentWorkerService.FindAgentAsync(worker, cancellationToken);

        if (agent is null)
        {
            return null;
        }

        var teamId = TeamOf(worker);
        var zone = await TimeZoneOfAsync(worker, cancellationToken);
        var fromUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var schedule = (await SchedulesAsync([worker.ItemId], fromUtc.AddHours(-12), toUtc.AddHours(12), cancellationToken)).GetValueOrDefault(worker.ItemId) ?? [];
        var spans = await _stateHistory.GetSpansAsync([agent.ItemId], fromUtc, toUtc, cancellationToken);
        var exceptions = await ListExceptionsAsync(worker.ItemId, fromUtc, toUtc, cancellationToken);
        var rules = await RulesForTeamAsync(teamId, cancellationToken);
        var result = AdherenceCalculator.Compute(
            schedule,
            spans.Select(span => new AdherenceActual { StartUtc = span.StartUtc, EndUtc = span.EndUtc, Status = span.Status, Reason = span.Reason }),
            rules,
            exceptions,
            fromUtc,
            toUtc);

        return new AdherenceDay
        {
            ItemId = IdGenerator.GenerateId(),
            WorkerId = worker.ItemId,
            TeamId = teamId,
            AgentId = agent.ItemId,
            Date = date,
            TimeZoneId = zone,
            ScheduledSeconds = result.ScheduledSeconds,
            InAdherenceSeconds = result.InAdherenceSeconds,
            ExceptionSeconds = result.ExceptionSeconds,
            ScheduledWorkSeconds = result.ScheduledWorkSeconds,
            ActualWorkSeconds = result.ActualWorkSeconds,
            Segments = result.Segments,
            ComputedUtc = _clock.UtcNow,
        };
    }

    /// <summary>
    /// Computes a worker's day and saves it in place of the one saved before.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The saved day, or <see langword="null"/> when the worker's user is not an agent.</returns>
    public async Task<AdherenceDay> SaveDayAsync(WorkerProfile worker, DateOnly date, CancellationToken cancellationToken = default)
    {
        var day = await ComputeDayAsync(worker, date, cancellationToken);

        if (day is null)
        {
            return null;
        }

        var existing = await FindDayAsync(worker.ItemId, date, cancellationToken);

        if (existing is not null)
        {
            day.ItemId = existing.ItemId;
            _session.Delete(existing, WorkforceConstants.CollectionName);
        }

        await _session.SaveAsync(day, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        return day;
    }

    /// <summary>
    /// Gets a worker's saved day.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="date">The day.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The day, or <see langword="null"/>.</returns>
    public Task<AdherenceDay> FindDayAsync(string workerId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var at = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        return _session.Query<AdherenceDay, AdherenceDayIndex>(index => index.WorkerId == workerId && index.Date == at, collection: WorkforceConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Computes the previous day of every worker whose user is an agent, when it isn't saved yet.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many days were saved.</returns>
    public async Task<int> SavePreviousDaysAsync(CancellationToken cancellationToken = default)
    {
        var saved = 0;
        var now = _clock.UtcNow;

        foreach (var (agent, worker) in await _agentWorkerService.ListAsync(cancellationToken))
        {
            if (worker is null || worker.Status != WorkerStatus.Active)
            {
                continue;
            }

            var zone = await TimeZoneOfAsync(worker, cancellationToken);
            var yesterday = DateOnly.FromDateTime(CalendarTime.ToLocal(now, zone)).AddDays(-1);

            if (await FindDayAsync(worker.ItemId, yesterday, cancellationToken) is null &&
                await SaveDayAsync(worker, yesterday, cancellationToken) is not null)
            {
                saved++;
            }
        }

        return saved;
    }

    /// <summary>
    /// Gets a worker's exceptions that overlap a stretch of time.
    /// </summary>
    /// <param name="workerId">The worker.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The exceptions.</returns>
    public async Task<IReadOnlyList<AdherenceException>> ListExceptionsAsync(string workerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        return [.. await _session.Query<AdherenceException, AdherenceExceptionIndex>(
                index => index.WorkerId == workerId && index.StartUtc < toUtc && index.EndUtc > fromUtc,
                collection: WorkforceConstants.CollectionName)
            .ListAsync(cancellationToken)];
    }

    /// <summary>
    /// Gets the exceptions of some teams, newest first, a page at a time.
    /// </summary>
    /// <param name="teamIds">The teams, or <see langword="null"/> for every team.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to take.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<AdherenceException> Items)> PageExceptionsAsync(IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _session.Query<AdherenceException, AdherenceExceptionIndex>(collection: WorkforceConstants.CollectionName);

        if (teamIds is not null)
        {
            var ids = teamIds.ToArray();
            query = query.Where(index => index.TeamId.IsIn(ids));
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(index => index.StartUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. items]);
    }

    /// <summary>
    /// Records an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The errors, or none when it was recorded.</returns>
    public async Task<IReadOnlyList<string>> AddExceptionAsync(AdherenceException exception, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var errors = new List<string>();
        var worker = await _workerManager.FindByIdAsync(exception.WorkerId, cancellationToken);

        if (worker is null)
        {
            errors.Add("The worker doesn't exist.");
        }

        if (exception.EndUtc <= exception.StartUtc)
        {
            errors.Add("The exception must end after it starts.");
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        exception.ItemId ??= IdGenerator.GenerateId();
        exception.TeamId ??= TeamOf(worker);
        exception.CreatedUtc = _clock.UtcNow;

        await _session.SaveAsync(exception, collection: WorkforceConstants.CollectionName, cancellationToken: cancellationToken);

        return errors;
    }

    /// <summary>
    /// Finds an exception.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The exception, or <see langword="null"/>.</returns>
    public Task<AdherenceException> FindExceptionAsync(string id, CancellationToken cancellationToken = default)
        => _session.Query<AdherenceException, AdherenceExceptionIndex>(index => index.ItemId == id, collection: WorkforceConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Removes an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    public void RemoveException(AdherenceException exception)
        => _session.Delete(exception, WorkforceConstants.CollectionName);

    /// <summary>
    /// Gets the published schedules of some workers over a stretch of time.
    /// </summary>
    /// <param name="workerIds">The workers.</param>
    /// <param name="fromUtc">The start.</param>
    /// <param name="toUtc">The end.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Each worker's scheduled pieces, in order.</returns>
    public async Task<Dictionary<string, List<AdherenceScheduledPiece>>> SchedulesAsync(IEnumerable<string> workerIds, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var ids = workerIds.ToHashSet(StringComparer.Ordinal);
        var occurrences = await _calendarQueryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            AllCalendars = true,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            EventTypes = [WorkforceConstants.EventTypes.Shift],
            Sources = [WorkforceConstants.Source],
        }, cancellationToken);

        return occurrences
            .Where(occurrence => occurrence.Event.Status != CalendarEventStatus.Cancelled)
            .Select(occurrence => (Occurrence: occurrence, Found: occurrence.Event.TryGet<ShiftData>(out var data), Data: data))
            .Where(entry => entry.Found && entry.Data is not null && ids.Contains(entry.Data.WorkerId))
            .GroupBy(entry => entry.Data.WorkerId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => AdherenceCalculator.BuildSchedule(group.Select(entry => new AdherenceShift
                {
                    StartUtc = entry.Occurrence.Event.StartUtc,
                    EndUtc = entry.Occurrence.Event.EndUtc,
                    Segments = [.. entry.Data.Segments],
                })),
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a worker's team: their primary team, else their first.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <returns>The team, or <see langword="null"/>.</returns>
    public static string TeamOf(WorkerProfile worker)
        => !string.IsNullOrEmpty(worker.PrimaryTeamId) ? worker.PrimaryTeamId : worker.TeamMemberships.FirstOrDefault()?.TeamId;

    /// <summary>
    /// Gets a worker's time zone: their own, else their team's or its parents', else the site's.
    /// </summary>
    /// <param name="worker">The worker.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The time zone.</returns>
    public async Task<string> TimeZoneOfAsync(WorkerProfile worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        return await _timeZones.GetAsync(worker, TeamOf(worker), cancellationToken) ?? "UTC";
    }

    private async Task<Dictionary<string, WorkforceTeam>> TeamsAsync(CancellationToken cancellationToken)
    {
        return _teams ??= (await _teamManager.ListTreeAsync(cancellationToken)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team, StringComparer.Ordinal);
    }
}

/// <summary>
/// An agent's row on the adherence board.
/// </summary>
public sealed class AdherenceBoardRow
{
    /// <summary>
    /// Gets or sets the worker's time zone.
    /// </summary>
    public string TimeZoneId { get; set; }

    /// <summary>
    /// Gets or sets the worker.
    /// </summary>
    public string WorkerId { get; set; }

    /// <summary>
    /// Gets or sets the worker's name.
    /// </summary>
    public string WorkerName { get; set; }

    /// <summary>
    /// Gets or sets the worker's team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the team's name.
    /// </summary>
    public string TeamName { get; set; }

    /// <summary>
    /// Gets or sets the agent profile.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the agent's state.
    /// </summary>
    public AgentPresenceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the state's reason.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets since when the agent is in the state.
    /// </summary>
    public DateTime? StatusSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets what is scheduled now.
    /// </summary>
    public AdherenceScheduledPiece Scheduled { get; set; }

    /// <summary>
    /// Gets or sets what is scheduled next.
    /// </summary>
    public AdherenceScheduledPiece Next { get; set; }

    /// <summary>
    /// Gets or sets whether the agent is in adherence.
    /// </summary>
    public bool InAdherence { get; set; }

    /// <summary>
    /// Gets or sets since when the agent is out of adherence.
    /// </summary>
    public DateTime? OutSinceUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the agent is out long enough to alert.
    /// </summary>
    public bool Alert { get; set; }
}
