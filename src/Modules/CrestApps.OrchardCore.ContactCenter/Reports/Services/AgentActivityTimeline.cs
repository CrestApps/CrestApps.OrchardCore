using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// What an agent was doing, in order: every state, offer, connection and call change, each with when it started,
/// when it ended and who made it.
/// </summary>
internal static class AgentActivityTimeline
{
    /// <summary>
    /// The agent connection events a timeline shows beside the agent's states.
    /// </summary>
    public static readonly string[] SessionEventTypes =
    [
        ContactCenterConstants.Events.AgentConnected,
        ContactCenterConstants.Events.AgentDisconnected,
        ContactCenterConstants.Events.AgentHeartbeatLost,
    ];

    /// <summary>
    /// The call events a timeline shows.
    /// </summary>
    public static readonly string[] CallEventTypes =
    [
        ContactCenterConstants.Events.CallQueued,
        ContactCenterConstants.Events.CallDequeued,
        ContactCenterConstants.Events.DialStarted,
        ContactCenterConstants.Events.DialFailed,
        ContactCenterConstants.Events.AgentLegAnswered,
        ContactCenterConstants.Events.AgentLegFailed,
        ContactCenterConstants.Events.CallConnected,
        ContactCenterConstants.Events.CallHeld,
        ContactCenterConstants.Events.CallResumed,
        ContactCenterConstants.Events.CallEnded,
        ContactCenterConstants.Events.CallAbandoned,
        ContactCenterConstants.Events.ConsultStarted,
        ContactCenterConstants.Events.ConsultConnected,
        ContactCenterConstants.Events.ConsultCompleted,
        ContactCenterConstants.Events.ConsultCancelled,
        ContactCenterConstants.Events.AiCallAnswered,
        ContactCenterConstants.Events.AiAnswererDetected,
        ContactCenterConstants.Events.AiConversationEnded,
        ContactCenterConstants.Events.AiHandoffRequested,
        ContactCenterConstants.Events.ExtensionCallStarted,
        ContactCenterConstants.Events.ExtensionCallEnded,
    ];

    // A call event whose duration is the span it closes: the hold on a resume, the wait on leaving a queue.
    private static readonly HashSet<string> _closingCallEvents = new(StringComparer.Ordinal)
    {
        ContactCenterConstants.Events.CallResumed,
        ContactCenterConstants.Events.CallDequeued,
        ContactCenterConstants.Events.CallAbandoned,
        ContactCenterConstants.Events.CallEnded,
        ContactCenterConstants.Events.ConsultCompleted,
        ContactCenterConstants.Events.ConsultCancelled,
        ContactCenterConstants.Events.AiConversationEnded,
        ContactCenterConstants.Events.ExtensionCallEnded,
    };

    /// <summary>
    /// Builds the timeline entries of a period, oldest first.
    /// </summary>
    /// <param name="timelines">The agents' state timelines.</param>
    /// <param name="sessionEvents">The agents' connection events.</param>
    /// <param name="offerEvents">The offer events, of any agent: only the timelines' agents' offers are shown.</param>
    /// <param name="callEvents">The call events, of any agent: the calls the timelines' agents worked are shown.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period: a state still held then ends at it.</param>
    /// <returns>The entries, in the order they started.</returns>
    public static IReadOnlyList<AgentActivityEntry> Build(
        IReadOnlyList<AgentStateTimeline> timelines,
        IEnumerable<InteractionEvent> sessionEvents,
        IEnumerable<InteractionEvent> offerEvents,
        IEnumerable<InteractionEvent> callEvents,
        DateTime fromUtc,
        DateTime toUtc)
        => SelectFirst(timelines, sessionEvents, offerEvents, callEvents, fromUtc, toUtc, int.MaxValue).Entries;

    /// <summary>
    /// Builds the first entries of a period, oldest first, and counts every entry of it.
    /// </summary>
    /// <param name="timelines">The agents' state timelines.</param>
    /// <param name="sessionEvents">The agents' connection events.</param>
    /// <param name="offerEvents">The offer events, of any agent: only the timelines' agents' offers are shown.</param>
    /// <param name="callEvents">The call events, of any agent: the calls the timelines' agents worked are shown.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period: a state still held then ends at it.</param>
    /// <param name="limit">The most entries to build.</param>
    /// <returns>
    /// The first <paramref name="limit"/> entries in the order they started, exactly the ones building every entry and
    /// keeping the first would give, and the counts of every entry.
    /// </returns>
    /// <remarks>
    /// Every entry is placed and counted, but only the ones listed are built: their detail text, which is most of the
    /// cost of an entry, is never written for an entry nobody sees. The first entries are kept in a bounded heap, so
    /// a wide period costs one pass over its entries rather than a sort of all of them.
    /// </remarks>
    public static AgentActivitySelection SelectFirst(
        IReadOnlyList<AgentStateTimeline> timelines,
        IEnumerable<InteractionEvent> sessionEvents,
        IEnumerable<InteractionEvent> offerEvents,
        IEnumerable<InteractionEvent> callEvents,
        DateTime fromUtc,
        DateTime toUtc,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(timelines);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        var agentIds = timelines.Select(timeline => timeline.AgentId).ToHashSet(StringComparer.Ordinal);
        var selector = new EntrySelector(limit);

        foreach (var timeline in timelines)
        {
            AddStates(selector, timeline, fromUtc, toUtc);
        }

        foreach (var sessionEvent in sessionEvents ?? [])
        {
            var data = sessionEvent.GetData<AgentSessionEventData>();

            if (data is null || sessionEvent.OccurredUtc < fromUtc || sessionEvent.OccurredUtc > toUtc)
            {
                continue;
            }

            selector.Add(AgentActivityKind.Connection, sessionEvent.OccurredUtc, RecordedUtc(sessionEvent), flagged: false, () =>
            {
                var detail = new List<string> { string.Create(CultureInfo.InvariantCulture, $"{data.OpenConnectionCount} connection(s) open") };

                if (data.LastHeartbeatUtc.HasValue)
                {
                    detail.Add("last heard from " + AuditReportFormat.Timestamp(data.LastHeartbeatUtc.Value));
                }

                if (!string.IsNullOrEmpty(data.Reason))
                {
                    detail.Add(data.Reason);
                }

                return Entry(sessionEvent, sessionEvent.AggregateId, AgentActivityKind.Connection, sessionEvent.EventType, string.Join("; ", detail), sessionEvent.OccurredUtc, null, null);
            });
        }

        var workedInteractions = new HashSet<string>(StringComparer.Ordinal);

        foreach (var offer in (offerEvents ?? [])
            .Select(offerEvent => (Event: offerEvent, Data: offerEvent.GetData<OfferLifecycleEventData>()))
            .Where(entry => entry.Data is not null && entry.Data.AgentId is not null && agentIds.Contains(entry.Data.AgentId))
            .GroupBy(entry => entry.Data.ReservationId ?? entry.Event.AggregateId, StringComparer.Ordinal))
        {
            var settled = offer.LastOrDefault(entry => !string.Equals(entry.Event.EventType, ContactCenterConstants.Events.OfferPresented, StringComparison.Ordinal));
            var shown = settled.Event is null ? offer.First() : settled;
            var presentedUtc = shown.Data.PresentedUtc ?? offer.First().Event.OccurredUtc;
            var settledUtc = settled.Event is null ? (DateTime?)null : shown.Data.SettledUtc ?? shown.Event.OccurredUtc;

            if (!string.IsNullOrEmpty(shown.Data.InteractionId))
            {
                workedInteractions.Add(shown.Data.InteractionId);
            }

            selector.Add(AgentActivityKind.Offer, presentedUtc, RecordedUtc(shown.Event), flagged: false, () => Entry(
                shown.Event,
                shown.Data.AgentId,
                AgentActivityKind.Offer,
                settled.Event is null ? ContactCenterConstants.Events.OfferPresented + " (not settled)" : shown.Event.EventType,
                Join(shown.Data.Channel, shown.Data.Reason),
                presentedUtc,
                settledUtc,
                shown.Data.InteractionId,
                shown.Data.RingSeconds));
        }

        // The agent a call no own agent is named on is shown under is the first timeline whose states name the call.
        var interactionAgents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var timeline in timelines)
        {
            foreach (var transition in timeline.Transitions)
            {
                if (!string.IsNullOrEmpty(transition.InteractionId))
                {
                    workedInteractions.Add(transition.InteractionId);
                    interactionAgents.TryAdd(transition.InteractionId, timeline.AgentId);
                }
            }
        }

        var fallbackAgentId = agentIds.FirstOrDefault();

        foreach (var callEvent in callEvents ?? [])
        {
            var data = callEvent.GetData<CallLifecycleEventData>();

            if (data is null || callEvent.OccurredUtc < fromUtc || callEvent.OccurredUtc > toUtc)
            {
                continue;
            }

            var interactionId = data.InteractionId ?? callEvent.InteractionId;
            var ownAgent = data.AgentId is not null && agentIds.Contains(data.AgentId);

            if (!ownAgent && (interactionId is null || !workedInteractions.Contains(interactionId)))
            {
                continue;
            }

            DateTime startUtc = callEvent.OccurredUtc;
            DateTime? endUtc = null;

            if (data.DurationSeconds.HasValue && _closingCallEvents.Contains(callEvent.EventType))
            {
                endUtc = callEvent.OccurredUtc;
                startUtc = callEvent.OccurredUtc.AddSeconds(-Math.Max(0, data.DurationSeconds.Value));
            }

            selector.Add(AgentActivityKind.Call, startUtc, RecordedUtc(callEvent), flagged: false, () =>
            {
                var agentId = ownAgent
                    ? data.AgentId
                    : interactionAgents.GetValueOrDefault(interactionId) ?? fallbackAgentId;

                return Entry(
                    callEvent,
                    agentId,
                    AgentActivityKind.Call,
                    callEvent.EventType,
                    Join(data.LegRole, data.State, data.HangupCause ?? data.ProviderHangupCause, data.Reason, data.Target),
                    startUtc,
                    endUtc,
                    interactionId,
                    endUtc.HasValue ? data.DurationSeconds : null);
            });
        }

        return selector.Complete();
    }

    private static void AddStates(EntrySelector selector, AgentStateTimeline timeline, DateTime fromUtc, DateTime toUtc)
    {
        var effective = timeline.Effective;

        for (var index = 0; index < effective.Count; index++)
        {
            var transition = effective[index];
            var endUtc = index + 1 < effective.Count ? effective[index + 1].ChangedUtc : toUtc;

            if (endUtc <= fromUtc || transition.ChangedUtc > toUtc)
            {
                continue;
            }

            var startUtc = transition.ChangedUtc < fromUtc ? fromUtc : transition.ChangedUtc;
            var clippedEnd = endUtc > toUtc ? toUtc : endUtc;

            selector.Add(AgentActivityKind.State, startUtc, transition.RecordedUtc, transition.BreaksChain, () =>
            {
                var detail = new List<string>();

                if (transition.ChangedUtc < fromUtc)
                {
                    detail.Add("in this state since " + AuditReportFormat.Timestamp(transition.ChangedUtc));
                }

                if (transition.BreaksChain)
                {
                    detail.Add(string.Create(CultureInfo.InvariantCulture, $"recorded as leaving {transition.PreviousState}, but the record before entered {transition.ExpectedPreviousState}: a change is missing"));
                }

                if (!transition.FromAudit)
                {
                    detail.Add("older presence event");
                }

                return new AgentActivityEntry
                {
                    AgentId = timeline.AgentId,
                    Kind = AgentActivityKind.State,
                    Name = transition.CurrentState.ToString(),
                    Detail = Join([Describe(transition), .. detail]),
                    StartUtc = startUtc,
                    EndUtc = clippedEnd,
                    DurationSeconds = (clippedEnd - startUtc).TotalSeconds,
                    InteractionId = transition.InteractionId,
                    ActorType = transition.ActorType,
                    ActorId = transition.ActorId,
                    RecordedUtc = transition.RecordedUtc,
                    Flagged = transition.BreaksChain,
                };
            });
        }

        // A change that happened after a sign-off dated by the last heartbeat is shown, because it was recorded, but
        // it takes no time: the agent was already gone.
        foreach (var transition in timeline.Transitions.Where(transition => transition.Superseded && transition.ChangedUtc >= fromUtc && transition.ChangedUtc <= toUtc))
        {
            selector.Add(AgentActivityKind.State, transition.ChangedUtc, transition.RecordedUtc, flagged: true, () => new AgentActivityEntry
            {
                AgentId = timeline.AgentId,
                Kind = AgentActivityKind.State,
                Name = transition.CurrentState.ToString(),
                Detail = Join(Describe(transition), "superseded: recorded before the sign-off dated earlier than it"),
                StartUtc = transition.ChangedUtc,
                InteractionId = transition.InteractionId,
                ActorType = transition.ActorType,
                ActorId = transition.ActorId,
                RecordedUtc = transition.RecordedUtc,
                Flagged = true,
            });
        }
    }

    private static DateTime RecordedUtc(InteractionEvent interactionEvent)
        => interactionEvent.RecordedUtc == default ? interactionEvent.OccurredUtc : interactionEvent.RecordedUtc;

    private static string Describe(AgentStateTransition transition)
        => Join(
            string.Create(CultureInfo.InvariantCulture, $"from {transition.PreviousState}"),
            transition.Source,
            transition.Reason,
            transition.RequestedState.HasValue ? "requested " + transition.RequestedState.Value : null);

    private static AgentActivityEntry Entry(
        InteractionEvent interactionEvent,
        string agentId,
        AgentActivityKind kind,
        string name,
        string detail,
        DateTime startUtc,
        DateTime? endUtc,
        string interactionId,
        double? durationSeconds = null)
        => new()
        {
            AgentId = agentId,
            Kind = kind,
            Name = name,
            Detail = detail,
            StartUtc = startUtc,
            EndUtc = endUtc,
            DurationSeconds = durationSeconds ?? (endUtc.HasValue ? Math.Max(0, (endUtc.Value - startUtc).TotalSeconds) : null),
            InteractionId = interactionId,
            ActorType = interactionEvent.ActorType,
            ActorId = interactionEvent.ActorId,
            RecordedUtc = interactionEvent.RecordedUtc == default ? interactionEvent.OccurredUtc : interactionEvent.RecordedUtc,
        };

    private static string Join(params string[] parts)
        => string.Join("; ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

    /// <summary>
    /// Keeps the first entries of a timeline in the order they started, building only those, and counts every entry.
    /// </summary>
    /// <remarks>
    /// The order is the one the whole timeline is sorted in: start, then when the change was recorded, then kind, and
    /// then the order the entries were offered in, which is what a stable sort of every entry falls back to.
    /// </remarks>
    private sealed class EntrySelector
    {
        private static readonly Comparer<Candidate> _latestFirst = Comparer<Candidate>.Create((left, right) => Compare(right, left));

        private readonly int _limit;
        private readonly PriorityQueue<Candidate, Candidate> _first;
        private long _sequence;
        private int _total;
        private int _states;
        private int _offers;
        private int _calls;
        private int _flagged;

        public EntrySelector(int limit)
        {
            _limit = limit;
            _first = new PriorityQueue<Candidate, Candidate>(_latestFirst);
        }

        public void Add(AgentActivityKind kind, DateTime startUtc, DateTime recordedUtc, bool flagged, Func<AgentActivityEntry> build)
        {
            _total++;

            switch (kind)
            {
                case AgentActivityKind.State:
                    _states++;
                    break;
                case AgentActivityKind.Offer:
                    _offers++;
                    break;
                case AgentActivityKind.Call:
                    _calls++;
                    break;
            }

            if (flagged)
            {
                _flagged++;
            }

            if (_limit == 0)
            {
                return;
            }

            var candidate = new Candidate(startUtc, recordedUtc, kind, _sequence++, build);

            if (_first.Count < _limit)
            {
                _first.Enqueue(candidate, candidate);

                return;
            }

            // The heap's top is the latest entry kept; an entry that sorts before it takes its place.
            if (Compare(candidate, _first.Peek()) < 0)
            {
                _first.DequeueEnqueue(candidate, candidate);
            }
        }

        public AgentActivitySelection Complete()
        {
            var kept = new List<Candidate>(_first.Count);

            while (_first.Count > 0)
            {
                kept.Add(_first.Dequeue());
            }

            kept.Sort(Compare);

            return new AgentActivitySelection
            {
                Entries = [.. kept.Select(candidate => candidate.Build())],
                TotalCount = _total,
                StateCount = _states,
                OfferCount = _offers,
                CallCount = _calls,
                FlaggedCount = _flagged,
            };
        }

        private static int Compare(Candidate left, Candidate right)
        {
            var order = left.StartUtc.CompareTo(right.StartUtc);

            if (order != 0)
            {
                return order;
            }

            order = left.RecordedUtc.CompareTo(right.RecordedUtc);

            if (order != 0)
            {
                return order;
            }

            order = left.Kind.CompareTo(right.Kind);

            return order != 0
                ? order
                : left.Sequence.CompareTo(right.Sequence);
        }

        private sealed record Candidate(DateTime StartUtc, DateTime RecordedUtc, AgentActivityKind Kind, long Sequence, Func<AgentActivityEntry> Build);
    }
}

/// <summary>
/// What a timeline entry is about.
/// </summary>
internal enum AgentActivityKind
{
    State,
    Offer,
    Call,
    Connection,
}

/// <summary>
/// One entry in an agent's activity timeline.
/// </summary>
internal sealed class AgentActivityEntry
{
    public string AgentId { get; init; }

    public AgentActivityKind Kind { get; init; }

    public string Name { get; init; }

    public string Detail { get; init; }

    public DateTime StartUtc { get; init; }

    public DateTime? EndUtc { get; init; }

    public double? DurationSeconds { get; init; }

    public string InteractionId { get; init; }

    public ContactCenterActorType ActorType { get; init; }

    public string ActorId { get; init; }

    public DateTime RecordedUtc { get; init; }

    public bool Flagged { get; init; }
}
