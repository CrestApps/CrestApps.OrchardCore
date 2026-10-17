using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Reads every agent's time over a period from the daily summaries where they cover it and from the log elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// A summary is used for a whole UTC day that starts after the period does and ends by the time the period does. The
/// first day of a period is always read from the log, so the period opens exactly as a report over the log opens it,
/// and so is a day that has not ended yet, a day without a summary, and the last, partial day.
/// </para>
/// <para>
/// A summary is used only while the log still holds the state changes it was computed from: each run of consecutive
/// summaries, and the summary of the day before the first one when there is one, is checked against a count of the
/// day's changes on the event index. A day the log has gained or lost changes for since its summary was computed, and
/// every day after it, are read from the log instead, because a change late in one day is the state the next day opens
/// in.
/// </para>
/// <para>
/// An interval is counted where it starts. A summary therefore counts the intervals that start on its day and only
/// carries the time of the one already running at midnight, and a stretch read from the log does the same, except at
/// the start of the period, where a report over the log counts the interval the period opens in.
/// </para>
/// </remarks>
internal sealed class AgentTimeSummaryReader : IAgentTimeSummaryReader
{
    private readonly ISession _session;
    private readonly IInteractionEventStore _eventStore;
    private readonly ContactCenterReportingOptions _options;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentTimeSummaryReader"/> class.
    /// </summary>
    /// <param name="session">The YesSql session the summaries and the event index are read through.</param>
    /// <param name="eventStore">The event log.</param>
    /// <param name="options">The reporting options, which can turn the summaries off.</param>
    /// <param name="logger">The logger.</param>
    public AgentTimeSummaryReader(
        ISession session,
        IInteractionEventStore eventStore,
        IOptions<ContactCenterReportingOptions> options,
        ILogger<AgentTimeSummaryReader> logger)
    {
        _session = session;
        _eventStore = eventStore;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AgentPresenceInterval>> ReadAsync(
        IReadOnlyCollection<string> agentIds,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agentIds);

        var summaries = await GetUsableSummariesAsync(fromUtc, toUtc, cancellationToken);

        // Every agent a report over the log looks up: the agents asked for, and every agent with a change in the period.
        var lookedUp = new HashSet<string>(agentIds.Where(agentId => !string.IsNullOrEmpty(agentId)), StringComparer.Ordinal);

        foreach (var summary in summaries)
        {
            lookedUp.UnionWith(summary.Agents.Where(agent => agent.HadEvents).Select(agent => agent.AgentId));
        }

        var stretches = new List<(DateTime From, DateTime End, DateTime Through)>();
        var cursor = fromUtc;

        foreach (var summary in summaries)
        {
            if (summary.Date > cursor)
            {
                stretches.Add((From: cursor, End: summary.Date, Through: summary.Date.AddTicks(-1)));
            }

            cursor = summary.Date.AddDays(1);
        }

        stretches.Add((From: cursor, End: toUtc, Through: toUtc));

        var accumulator = new Accumulator(fromUtc);
        var included = new HashSet<string>(lookedUp, StringComparer.Ordinal);
        var read = new List<((DateTime From, DateTime End, DateTime Through) Stretch, AgentTimeStretch Result)>(stretches.Count);

        foreach (var stretch in stretches)
        {
            var result = await AgentDailyTimeCalculator.ReadAsync(
                _eventStore,
                lookedUp,
                stretch.From,
                stretch.End,
                stretch.Through,
                lookbehind: stretch.From != fromUtc,
                onlyTheseAgents: false,
                cancellationToken);

            included.UnionWith(result.AgentsWithEvents);
            read.Add((Stretch: stretch, Result: result));
            accumulator.Add(result.Intervals);
        }

        // An agent whose only changes fall in one stretch, and who is no longer an agent profile, was not looked up in
        // the other stretches; a report over the log looks it up for the whole period, so it is read there too.
        var foundLate = included.Where(agentId => !lookedUp.Contains(agentId)).ToArray();

        if (foundLate.Length > 0)
        {
            foreach (var (stretch, result) in read)
            {
                var present = result.Intervals.Select(interval => interval.AgentId).ToHashSet(StringComparer.Ordinal);
                var missing = foundLate.Where(agentId => !present.Contains(agentId) && !result.AgentsWithEvents.Contains(agentId)).ToArray();

                if (missing.Length == 0)
                {
                    continue;
                }

                var completion = await AgentDailyTimeCalculator.ReadAsync(
                    _eventStore,
                    missing,
                    stretch.From,
                    stretch.End,
                    stretch.Through,
                    lookbehind: stretch.From != fromUtc,
                    onlyTheseAgents: true,
                    cancellationToken);

                accumulator.Add(completion.Intervals);
            }
        }

        foreach (var summary in summaries)
        {
            foreach (var agent in summary.Agents)
            {
                accumulator.Add(agent, summary.Date);
            }
        }

        return accumulator.ToIntervals(included);
    }

    private async Task<IReadOnlyList<AgentDailyTimeSummary>> GetUsableSummariesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (_options.AgentTimeSummaryBackfill <= TimeSpan.Zero)
        {
            return [];
        }

        // A day is read from its summary only when it starts after the period does and ends by the time it does.
        var firstDay = AgentDailyTimeCalculator.DayStart(fromUtc).AddDays(1);
        var lastDay = AgentDailyTimeCalculator.DayStart(toUtc.AddDays(-1));

        if (lastDay < firstDay)
        {
            return [];
        }

        var checkDay = firstDay.AddDays(-1);
        var version = AgentDailyTimeCalculator.Version;

        var rows = await _session.QueryIndex<AgentDailyTimeSummaryIndex>(
                index => index.Date >= checkDay && index.Date <= lastDay && index.Version == version,
                collection: ContactCenterStorage.CollectionName)
            .ListAsync(cancellationToken);

        var byDay = rows
            .GroupBy(row => AgentDailyTimeCalculator.DayStart(row.Date))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(row => row.DocumentId).First());

        var usable = new List<long>();
        var day = checkDay;

        while (day <= lastDay)
        {
            if (!byDay.ContainsKey(day))
            {
                day = day.AddDays(1);

                continue;
            }

            var run = new List<(DateTime Day, AgentDailyTimeSummaryIndex Row)>();

            while (day <= lastDay && byDay.TryGetValue(day, out var row))
            {
                run.Add((Day: day, Row: row));
                day = day.AddDays(1);
            }

            foreach (var (runDay, row) in await VerifyAsync(run, cancellationToken))
            {
                if (runDay >= firstDay)
                {
                    usable.Add(row.DocumentId);
                }
            }
        }

        if (usable.Count == 0)
        {
            return [];
        }

        var summaries = await _session.GetAsync<AgentDailyTimeSummary>([.. usable], ContactCenterStorage.CollectionName, cancellationToken);

        return [.. summaries.OrderBy(summary => summary.Date)];
    }

    private async Task<IReadOnlyList<(DateTime Day, AgentDailyTimeSummaryIndex Row)>> VerifyAsync(
        List<(DateTime Day, AgentDailyTimeSummaryIndex Row)> run,
        CancellationToken cancellationToken)
    {
        var from = run[0].Day;
        var through = run[run.Count - 1].Day.AddDays(1).AddTicks(-1);
        var expected = run.Sum(entry => (long)entry.Row.EventCount);
        var actual = await AgentDailyTimeCalculator.CountStateEventsAsync(_session, from, through, cancellationToken);

        if (actual == expected)
        {
            return run;
        }

        for (var index = 0; index < run.Count; index++)
        {
            var (runDay, row) = run[index];
            var count = await AgentDailyTimeCalculator.CountStateEventsAsync(_session, runDay, runDay.AddDays(1).AddTicks(-1), cancellationToken);

            if (count != row.EventCount)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "The agent time summary of {Day:yyyy-MM-dd} was computed from {Expected} state changes but the log now holds {Actual}, so that day and the days after it are read from the log until the summaries are recomputed.",
                        runDay,
                        row.EventCount,
                        count);
                }

                return run.Take(index).ToArray();
            }
        }

        // The run as a whole disagrees with the log while no day of it does, which the counts cannot explain, so none of
        // it is trusted.
        return [];
    }

    /// <summary>
    /// Adds up time and interval counts per agent and per state, reason, queue set and campaign set.
    /// </summary>
    private sealed class Accumulator
    {
        private readonly DateTime _periodStartUtc;
        private readonly Dictionary<(string AgentId, string Key), Entry> _entries = [];

        public Accumulator(DateTime periodStartUtc)
        {
            _periodStartUtc = periodStartUtc;
        }

        public void Add(IEnumerable<AgentPresenceInterval> intervals)
        {
            foreach (var interval in intervals)
            {
                var entry = GetOrAdd(interval.AgentId, interval.Status, interval.Reason, interval.QueueIds, interval.CampaignIds);

                entry.Ticks += (interval.EndUtc - interval.StartUtc).Ticks;

                // The interval the period opens in is counted, as a report over the log counts it; an interval a stretch
                // only continues was counted where it started.
                if (!interval.ContinuesFromEarlier || interval.StartUtc == _periodStartUtc)
                {
                    entry.Count++;
                }
            }
        }

        public void Add(AgentDailyTime agent, DateTime dayStartUtc)
        {
            foreach (var bucket in agent.Buckets)
            {
                var entry = GetOrAdd(agent.AgentId, bucket.Status, bucket.Reason, bucket.QueueIds, bucket.CampaignIds);

                entry.Ticks += bucket.Ticks;
                entry.Count += bucket.Continued
                    ? (dayStartUtc == _periodStartUtc ? 1 : 0)
                    : bucket.Count;
            }
        }

        public List<AgentPresenceInterval> ToIntervals(HashSet<string> agentIds)
        {
            var intervals = new List<AgentPresenceInterval>();

            foreach (var entry in _entries
                .Where(pair => agentIds.Contains(pair.Key.AgentId))
                .OrderBy(pair => pair.Key.AgentId, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value))
            {
                if (entry.Ticks <= 0 && entry.Count == 0)
                {
                    continue;
                }

                // The time is shared among as many intervals as were counted, in whole seconds where it is whole seconds,
                // so the totals a report adds up are the same totals, and the same number of intervals.
                var count = Math.Max(1L, entry.Count);
                var wholeSeconds = entry.Ticks / TimeSpan.TicksPerSecond;
                var remainder = entry.Ticks % TimeSpan.TicksPerSecond;

                for (var index = 0L; index < count; index++)
                {
                    var seconds = (wholeSeconds / count) + (index < wholeSeconds % count ? 1 : 0);
                    var ticks = (seconds * TimeSpan.TicksPerSecond) + (index == 0 ? remainder : 0);

                    intervals.Add(new AgentPresenceInterval
                    {
                        AgentId = entry.AgentId,
                        Status = entry.Status,
                        Reason = entry.Reason,
                        QueueIds = [.. entry.QueueIds],
                        CampaignIds = [.. entry.CampaignIds],
                        StartUtc = _periodStartUtc,
                        EndUtc = _periodStartUtc.AddTicks(ticks),
                    });
                }
            }

            return intervals;
        }

        private Entry GetOrAdd(
            string agentId,
            AgentPresenceStatus status,
            string reason,
            IEnumerable<string> queueIds,
            IEnumerable<string> campaignIds)
        {
            var key = (AgentId: agentId, Key: AgentDailyTimeCalculator.Key(status, reason, queueIds, campaignIds));

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new Entry
                {
                    AgentId = agentId,
                    Status = status,
                    Reason = reason,
                    QueueIds = [.. queueIds ?? []],
                    CampaignIds = [.. campaignIds ?? []],
                };

                _entries.Add(key, entry);
            }

            return entry;
        }
    }

    private sealed class Entry
    {
        public string AgentId { get; init; }

        public AgentPresenceStatus Status { get; init; }

        public string Reason { get; init; }

        public string[] QueueIds { get; init; }

        public string[] CampaignIds { get; init; }

        public long Ticks { get; set; }

        public long Count { get; set; }
    }
}
