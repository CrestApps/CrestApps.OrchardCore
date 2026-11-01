using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.IntervalStatistics;

/// <summary>
/// Keeps the interval statistics up to date, a few days per run: a rebuild an administrator asked for first, then
/// today, the days not yet final, recent days whose interactions changed, and the days not computed yet, newest
/// first. It deletes the days that fall out of the retention window.
/// </summary>
/// <remarks>
/// It runs from one place at a time, the background task, whose lock makes it single-writer across nodes. Each day is
/// written with the state's cursor in one transaction, so a rebuild stopped part way resumes from the day after the
/// last one written. The state is saved with a concurrency check, so a rebuild asked for during a run is never lost.
/// </remarks>
public sealed class IntervalStatisticsRoller
{
    private const int MaxDeletionsPerRun = 100;
    private const int DaysVerified = 3;

    private static readonly TimeSpan _verifyEvery = TimeSpan.FromHours(1);

    private readonly ISession _session;
    private readonly IInteractionEventStore _eventStore;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IntervalStatisticsOptions _options;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalStatisticsRoller"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="eventStore">The event log.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="agentManager">The agent directory.</param>
    /// <param name="options">The interval statistics options.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public IntervalStatisticsRoller(
        ISession session,
        IInteractionEventStore eventStore,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        IOptions<IntervalStatisticsOptions> options,
        IClock clock,
        ILogger<IntervalStatisticsRoller> logger)
    {
        _session = session;
        _eventStore = eventStore;
        _queueManager = queueManager;
        _agentManager = agentManager;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets the state: the rebuild and the last run.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The state; a new one when there is none yet.</returns>
    /// <remarks>
    /// A commit detaches what the session loaded, so the state is loaded again after every commit before it is
    /// changed: saving a detached state would add a second one.
    /// </remarks>
    public async Task<IntervalStatisticsState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var states = (await _session.Query(collection: ContactCenterStorage.CollectionName)
            .For<IntervalStatisticsState>()
            .ListAsync(cancellationToken)).ToArray();

        // Only one state is kept. Any other is a copy an earlier save added and is never read.
        foreach (var extra in states.Skip(1))
        {
            _session.Delete(extra, ContactCenterStorage.CollectionName);
        }

        return states.Length > 0 ? states[0] : new IntervalStatisticsState { ItemId = IdGenerator.GenerateId() };
    }

    /// <summary>
    /// Asks for a range of days to be computed again. The days are inside the retention window and not after today.
    /// </summary>
    /// <param name="fromUtc">Any instant of the first day.</param>
    /// <param name="toUtc">Any instant of the last day.</param>
    /// <param name="requestedBy">Who asked.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The state with the rebuild.</returns>
    public async Task<IntervalStatisticsState> RequestRebuildAsync(DateTime fromUtc, DateTime toUtc, string requestedBy, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var today = IntervalStatisticsConstants.DayStart(now);
        var earliest = today.AddDays(-_options.RetentionDays);
        var from = IntervalStatisticsConstants.DayStart(fromUtc);
        var to = IntervalStatisticsConstants.DayStart(toUtc);

        if (from < earliest)
        {
            from = earliest;
        }

        if (to > today)
        {
            to = today;
        }

        if (to < from)
        {
            throw new ArgumentException("The last day of a rebuild cannot be before its first day.", nameof(toUtc));
        }

        var state = await GetStateAsync(cancellationToken);

        state.RebuildFromUtc = from;
        state.RebuildToUtc = to;
        state.RebuildNextUtc = from;
        state.RebuildRequestedBy = requestedBy;
        state.RebuildRequestedUtc = now;

        await _session.SaveAsync(state, checkConcurrency: true, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return state;
    }

    /// <summary>
    /// Brings the interval statistics up to date by one bounded step.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many days were computed or deleted.</returns>
    public async Task<int> RollAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var changes = await DeleteExpiredAsync(now, cancellationToken);
        var budget = Math.Max(1, _options.MaxDaysPerRun);
        var today = IntervalStatisticsConstants.DayStart(now);
        var state = await GetStateAsync(cancellationToken);

        while (state.IsRebuilding && budget > 0)
        {
            var day = state.RebuildNextUtc.Value;

            state.RebuildNextUtc = day.AddDays(1);
            state.LastProgressUtc = now;

            if (!state.IsRebuilding)
            {
                state.RebuildFromUtc = null;
                state.RebuildToUtc = null;
                state.RebuildNextUtc = null;
            }

            if (day <= today)
            {
                await ComputeDayAsync(day, now, state, cancellationToken);
                changes++;
            }
            else
            {
                await _session.SaveAsync(state, checkConcurrency: true, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }

            state = await GetStateAsync(cancellationToken);
            budget--;
        }

        foreach (var day in await DaysDueAsync(now, budget, cancellationToken))
        {
            state.LastProgressUtc = now;
            await ComputeDayAsync(day, now, state, cancellationToken);
            state = await GetStateAsync(cancellationToken);
            changes++;
        }

        state.LastRunUtc = now;
        state.LastError = null;
        await _session.SaveAsync(state, checkConcurrency: true, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        if (changes > 0 && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Computed or deleted {Count} day(s) of Contact Center interval statistics.", changes);
        }

        return changes;
    }

    /// <summary>
    /// Computes one day and writes it, with the state, in one transaction.
    /// </summary>
    /// <param name="dayUtc">Any instant of the day.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <param name="state">The state to save with the day.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the day is written.</returns>
    internal async Task ComputeDayAsync(DateTime dayUtc, DateTime nowUtc, IntervalStatisticsState state, CancellationToken cancellationToken)
    {
        var day = IntervalStatisticsConstants.DayStart(dayUtc);
        var dayEnd = day.AddDays(1);
        var through = nowUtc < dayEnd ? nowUtc : dayEnd;

        var interactions = (await _session.Query<Interaction, InteractionIndex>(
                index => index.CreatedUtc >= day && index.CreatedUtc < dayEnd,
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken)).ToArray();

        var queues = (await _queueManager.GetAllAsync(cancellationToken))
            .Where(queue => !string.IsNullOrEmpty(queue.ItemId))
            .GroupBy(queue => queue.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var agents = (await _agentManager.GetAllAsync(cancellationToken))
            .Where(agent => !string.IsNullOrEmpty(agent.ItemId))
            .GroupBy(agent => agent.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var outcomes = await InteractionOutcomeClassifier.LoadAsync(_eventStore, interactions, cancellationToken);
        var stretch = through > day
            ? await AgentDailyTimeCalculator.ReadAsync(_eventStore, agents.Keys, day, through, through.AddTicks(-1), lookbehind: true, onlyTheseAgents: false, cancellationToken)
            : null;

        var queueDays = IntervalStatisticsCalculator.ComputeQueues(day, interactions, queues, outcomes, _options.ShortAbandonSeconds);
        var agentDays = IntervalStatisticsCalculator.ComputeAgents(day, stretch?.Intervals ?? [], interactions, agents, outcomes);

        await DeleteDayAsync(day, dayEnd, cancellationToken);

        foreach (var queueDay in queueDays)
        {
            await _session.SaveAsync(queueDay, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
        }

        foreach (var agentDay in agentDays)
        {
            await _session.SaveAsync(agentDay, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
        }

        await _session.SaveAsync(new IntervalStatisticsDay
        {
            ItemId = IdGenerator.GenerateId(),
            Date = day,
            Version = IntervalStatisticsConstants.Version,
            Final = nowUtc >= dayEnd + _options.SettleDelay,
            InteractionCount = interactions.Length,
            ComputedUtc = nowUtc,
        }, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);

        await _session.SaveAsync(state, checkConcurrency: true, collection: ContactCenterStorage.CollectionName, cancellationToken: cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<DateTime>> DaysDueAsync(DateTime now, int budget, CancellationToken cancellationToken)
    {
        var due = new List<DateTime>();

        if (budget <= 0)
        {
            return due;
        }

        var today = IntervalStatisticsConstants.DayStart(now);
        var earliest = today.AddDays(-Math.Min(_options.BackfillDays, _options.RetentionDays));
        var first = await FirstActivityUtcAsync(cancellationToken);

        // Nothing before the first interaction or agent state change is computed, so a new site starts with today.
        var firstDay = first.HasValue ? IntervalStatisticsConstants.DayStart(first.Value) : today;

        if (firstDay > earliest)
        {
            earliest = firstDay;
        }

        var version = IntervalStatisticsConstants.Version;
        var computed = (await _session.QueryIndex<IntervalStatisticsDayIndex>(
                index => index.Date >= earliest && index.Date <= today && index.Version == version,
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken))
            .GroupBy(index => IntervalStatisticsConstants.DayStart(DateTime.SpecifyKind(index.Date, DateTimeKind.Utc)))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(index => index.DocumentId).First());

        void Add(DateTime day)
        {
            if (due.Count < budget && !due.Contains(day))
            {
                due.Add(day);
            }
        }

        // Today, as often as the refresh allows.
        if (!computed.TryGetValue(today, out var current) || now - DateTime.SpecifyKind(current.ComputedUtc, DateTimeKind.Utc) >= _options.CurrentDayRefresh)
        {
            Add(today);
        }

        // The days not final yet: computed again as often as today, and once more when they become final.
        foreach (var (day, row) in computed.Where(entry => entry.Key < today && !entry.Value.Final).OrderByDescending(entry => entry.Key))
        {
            var settled = now >= day.AddDays(1) + _options.SettleDelay;

            if (settled || now - DateTime.SpecifyKind(row.ComputedUtc, DateTimeKind.Utc) >= _options.CurrentDayRefresh)
            {
                Add(day);
            }
        }

        // The last few final days, when interactions were added or removed since.
        foreach (var (day, row) in computed.Where(entry => entry.Key < today && entry.Key >= today.AddDays(-DaysVerified) && entry.Value.Final).OrderByDescending(entry => entry.Key))
        {
            if (due.Count >= budget || now - DateTime.SpecifyKind(row.ComputedUtc, DateTimeKind.Utc) < _verifyEvery)
            {
                continue;
            }

            var dayEnd = day.AddDays(1);
            var count = await _session.QueryIndex<InteractionIndex>(
                    index => index.CreatedUtc >= day && index.CreatedUtc < dayEnd,
                    collection: ContactCenterStorage.CollectionName)
                .CountAsync(cancellationToken);

            if (count != row.InteractionCount)
            {
                Add(day);
            }
        }

        // The days not computed yet, newest first.
        for (var day = today.AddDays(-1); day >= earliest && due.Count < budget; day = day.AddDays(-1))
        {
            if (!computed.ContainsKey(day))
            {
                Add(day);
            }
        }

        return due;
    }

    private async Task<DateTime?> FirstActivityUtcAsync(CancellationToken cancellationToken)
    {
        var interaction = await _session.QueryIndex<InteractionIndex>(collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var types = AgentStateTimeline.StateEventTypes;
        var stateChange = await _session.QueryIndex<InteractionEventIndex>(
                index => index.AggregateType == nameof(AgentProfile) && index.EventType.IsIn(types),
                collection: ContactCenterStorage.CollectionName)
            .OrderBy(index => index.OccurredUtc)
            .FirstOrDefaultAsync(cancellationToken);

        DateTime? first = interaction?.CreatedUtc;

        if (stateChange is not null && (first is null || stateChange.OccurredUtc < first))
        {
            first = stateChange.OccurredUtc;
        }

        return first.HasValue ? DateTime.SpecifyKind(first.Value, DateTimeKind.Utc) : null;
    }

    private async Task DeleteDayAsync(DateTime day, DateTime dayEnd, CancellationToken cancellationToken)
    {
        foreach (var queueDay in await _session.Query<QueueIntervalStatistics, QueueIntervalStatisticsIndex>(index => index.Date >= day && index.Date < dayEnd, collection: ContactCenterStorage.CollectionName).ListAsync(cancellationToken))
        {
            _session.Delete(queueDay, ContactCenterStorage.CollectionName);
        }

        foreach (var agentDay in await _session.Query<AgentIntervalStatistics, AgentIntervalStatisticsIndex>(index => index.Date >= day && index.Date < dayEnd, collection: ContactCenterStorage.CollectionName).ListAsync(cancellationToken))
        {
            _session.Delete(agentDay, ContactCenterStorage.CollectionName);
        }

        foreach (var marker in await _session.Query<IntervalStatisticsDay, IntervalStatisticsDayIndex>(index => index.Date >= day && index.Date < dayEnd, collection: ContactCenterStorage.CollectionName).ListAsync(cancellationToken))
        {
            _session.Delete(marker, ContactCenterStorage.CollectionName);
        }
    }

    private async Task<int> DeleteExpiredAsync(DateTime now, CancellationToken cancellationToken)
    {
        var cutoff = IntervalStatisticsConstants.DayStart(now).AddDays(-_options.RetentionDays);
        var deleted = 0;

        foreach (var queueDay in await _session.Query<QueueIntervalStatistics, QueueIntervalStatisticsIndex>(index => index.Date < cutoff, collection: ContactCenterStorage.CollectionName).Take(MaxDeletionsPerRun).ListAsync(cancellationToken))
        {
            _session.Delete(queueDay, ContactCenterStorage.CollectionName);
            deleted++;
        }

        foreach (var agentDay in await _session.Query<AgentIntervalStatistics, AgentIntervalStatisticsIndex>(index => index.Date < cutoff, collection: ContactCenterStorage.CollectionName).Take(MaxDeletionsPerRun).ListAsync(cancellationToken))
        {
            _session.Delete(agentDay, ContactCenterStorage.CollectionName);
            deleted++;
        }

        foreach (var marker in await _session.Query<IntervalStatisticsDay, IntervalStatisticsDayIndex>(index => index.Date < cutoff, collection: ContactCenterStorage.CollectionName).Take(MaxDeletionsPerRun).ListAsync(cancellationToken))
        {
            _session.Delete(marker, ContactCenterStorage.CollectionName);
            deleted++;
        }

        if (deleted > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        return deleted;
    }
}
