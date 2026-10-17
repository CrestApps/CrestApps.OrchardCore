using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using OrchardCore;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Computes agent time over a stretch of the agent state log, the one way both a report and the daily summaries do it,
/// so a day read from its summary and a day read from the log are the same numbers.
/// </summary>
internal static class AgentDailyTimeCalculator
{
    /// <summary>
    /// The version of the computation. A summary written by another version is not read, and the roller replaces it.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// How much of the log before a stretch is read with it, so a sign-off dated by the agent's last heartbeat shortly
    /// before the stretch, and recorded only after the stretch began, still supersedes what was recorded in between.
    /// A report over a longer period reads that time anyway; a stretch that starts mid-period must too.
    /// </summary>
    public static readonly TimeSpan Lookbehind = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets the UTC midnight that starts the day an instant falls on.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns>The start of its UTC day.</returns>
    public static DateTime DayStart(DateTime utc)
        => DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);

    /// <summary>
    /// Reads and computes the agents' state intervals over a stretch of the log.
    /// </summary>
    /// <param name="eventStore">The event log.</param>
    /// <param name="agentIds">The agents whose state before the stretch is looked up; every agent with a change in
    /// the stretch is read too.</param>
    /// <param name="fromUtc">The start of the stretch.</param>
    /// <param name="endUtc">Where the intervals end: the start of the next stretch, or the end of the period.</param>
    /// <param name="throughUtc">The last instant whose changes belong to the stretch.</param>
    /// <param name="lookbehind">Whether to read <see cref="Lookbehind"/> of the log before the stretch with it. A
    /// stretch that starts the period does not, exactly like a report over the period.</param>
    /// <param name="onlyTheseAgents">Whether only <paramref name="agentIds"/> are read, rather than every agent with a
    /// change in the stretch as well.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stretch's intervals and what was read.</returns>
    public static async Task<AgentTimeStretch> ReadAsync(
        IInteractionEventStore eventStore,
        IEnumerable<string> agentIds,
        DateTime fromUtc,
        DateTime endUtc,
        DateTime throughUtc,
        bool lookbehind,
        bool onlyTheseAgents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventStore);

        var events = await AgentStateEventReader.ReadAsync(
            eventStore,
            agentIds,
            onlyTheseAgents,
            lookbehind ? fromUtc - Lookbehind : fromUtc,
            throughUtc,
            additionalEventTypes: null,
            cancellationToken);

        var intervals = AgentStateTimeline.BuildIntervals(AgentStateTimeline.Build(events), fromUtc, endUtc);
        var agentsWithEvents = new HashSet<string>(StringComparer.Ordinal);
        var eventCount = 0;

        foreach (var interactionEvent in events)
        {
            if (interactionEvent.OccurredUtc < fromUtc || interactionEvent.OccurredUtc > throughUtc)
            {
                continue;
            }

            eventCount++;

            if (!string.IsNullOrEmpty(interactionEvent.AggregateId))
            {
                agentsWithEvents.Add(interactionEvent.AggregateId);
            }
        }

        return new AgentTimeStretch(intervals, agentsWithEvents, eventCount, events.Count);
    }

    /// <summary>
    /// Computes one UTC day's summary from the log.
    /// </summary>
    /// <param name="eventStore">The event log.</param>
    /// <param name="agentIds">The agents whose state before the day is looked up.</param>
    /// <param name="dayUtc">Any instant of the day.</param>
    /// <param name="computedUtc">When the summary is computed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The summary, not yet saved.</returns>
    public static async Task<AgentDailyTimeSummary> ComputeDayAsync(
        IInteractionEventStore eventStore,
        IEnumerable<string> agentIds,
        DateTime dayUtc,
        DateTime computedUtc,
        CancellationToken cancellationToken)
    {
        var dayStart = DayStart(dayUtc);
        var dayEnd = dayStart.AddDays(1);
        var stretch = await ReadAsync(eventStore, agentIds, dayStart, dayEnd, dayEnd.AddTicks(-1), lookbehind: true, onlyTheseAgents: false, cancellationToken);

        return new AgentDailyTimeSummary
        {
            ItemId = IdGenerator.GenerateId(),
            Date = dayStart,
            Version = Version,
            EventCount = stretch.EventCount,
            ComputedUtc = computedUtc,
            Agents = Summarize(stretch),
        };
    }

    /// <summary>
    /// Counts the agent state changes the log holds for a stretch, from the event index alone.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="fromUtc">The first instant of the stretch.</param>
    /// <param name="throughUtc">The last instant of the stretch.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of state changes.</returns>
    public static Task<int> CountStateEventsAsync(
        ISession session,
        DateTime fromUtc,
        DateTime throughUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        var types = AgentStateTimeline.StateEventTypes;

        return session.QueryIndex<InteractionEventIndex>(
                index => index.AggregateType == nameof(AgentProfile) &&
                    index.OccurredUtc >= fromUtc &&
                    index.OccurredUtc <= throughUtc &&
                    index.EventType.IsIn(types),
                collection: ContactCenterStorage.CollectionName)
            .CountAsync(cancellationToken);
    }

    /// <summary>
    /// Builds the key a bucket is grouped by: its state, reason, queues and campaigns.
    /// </summary>
    /// <param name="status">The state.</param>
    /// <param name="reason">The reason.</param>
    /// <param name="queueIds">The queues.</param>
    /// <param name="campaignIds">The campaigns.</param>
    /// <returns>The key.</returns>
    public static string Key(
        AgentPresenceStatus status,
        string reason,
        IEnumerable<string> queueIds,
        IEnumerable<string> campaignIds)
        => string.Join(
            '\u001f',
            ((int)status).ToString(CultureInfo.InvariantCulture),
            reason is null ? "\u0000" : "r" + reason,
            queueIds is null ? string.Empty : string.Join('\u001e', queueIds),
            campaignIds is null ? string.Empty : string.Join('\u001e', campaignIds));

    private static List<AgentDailyTime> Summarize(AgentTimeStretch stretch)
    {
        var agents = new Dictionary<string, AgentDailyTime>(StringComparer.Ordinal);
        var buckets = new Dictionary<(string AgentId, bool Continued, string Key), AgentDailyTimeBucket>();

        foreach (var interval in stretch.Intervals)
        {
            var agent = GetOrAdd(agents, interval.AgentId);
            var key = (AgentId: interval.AgentId, Continued: interval.ContinuesFromEarlier, Key: Key(interval.Status, interval.Reason, interval.QueueIds, interval.CampaignIds));

            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new AgentDailyTimeBucket
                {
                    Status = interval.Status,
                    Reason = interval.Reason,
                    QueueIds = [.. interval.QueueIds],
                    CampaignIds = [.. interval.CampaignIds],
                    Continued = interval.ContinuesFromEarlier,
                };

                buckets.Add(key, bucket);
                agent.Buckets.Add(bucket);
            }

            bucket.Ticks += (interval.EndUtc - interval.StartUtc).Ticks;

            if (!interval.ContinuesFromEarlier)
            {
                bucket.Count++;
            }
        }

        foreach (var agentId in stretch.AgentsWithEvents)
        {
            GetOrAdd(agents, agentId).HadEvents = true;
        }

        return [.. agents.Values.OrderBy(agent => agent.AgentId, StringComparer.Ordinal)];
    }

    private static AgentDailyTime GetOrAdd(Dictionary<string, AgentDailyTime> agents, string agentId)
    {
        if (!agents.TryGetValue(agentId, out var agent))
        {
            agent = new AgentDailyTime
            {
                AgentId = agentId,
            };

            agents.Add(agentId, agent);
        }

        return agent;
    }
}
