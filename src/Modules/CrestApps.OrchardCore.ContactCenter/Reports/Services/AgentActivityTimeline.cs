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
    {
        ArgumentNullException.ThrowIfNull(timelines);

        var agentIds = timelines.Select(timeline => timeline.AgentId).ToHashSet(StringComparer.Ordinal);
        var entries = new List<AgentActivityEntry>();

        foreach (var timeline in timelines)
        {
            AddStates(entries, timeline, fromUtc, toUtc);
        }

        foreach (var sessionEvent in sessionEvents ?? [])
        {
            var data = sessionEvent.GetData<AgentSessionEventData>();

            if (data is null || sessionEvent.OccurredUtc < fromUtc || sessionEvent.OccurredUtc > toUtc)
            {
                continue;
            }

            var detail = new List<string> { string.Create(CultureInfo.InvariantCulture, $"{data.OpenConnectionCount} connection(s) open") };

            if (data.LastHeartbeatUtc.HasValue)
            {
                detail.Add("last heard from " + AuditReportFormat.Timestamp(data.LastHeartbeatUtc.Value));
            }

            if (!string.IsNullOrEmpty(data.Reason))
            {
                detail.Add(data.Reason);
            }

            entries.Add(Entry(sessionEvent, sessionEvent.AggregateId, AgentActivityKind.Connection, sessionEvent.EventType, string.Join("; ", detail), sessionEvent.OccurredUtc, null, null));
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

            entries.Add(Entry(
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

        foreach (var transition in timelines.SelectMany(timeline => timeline.Transitions))
        {
            if (!string.IsNullOrEmpty(transition.InteractionId))
            {
                workedInteractions.Add(transition.InteractionId);
            }
        }

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

            var agentId = ownAgent
                ? data.AgentId
                : timelines.FirstOrDefault(timeline => timeline.Transitions.Any(transition => string.Equals(transition.InteractionId, interactionId, StringComparison.Ordinal)))?.AgentId
                    ?? agentIds.FirstOrDefault();

            entries.Add(Entry(
                callEvent,
                agentId,
                AgentActivityKind.Call,
                callEvent.EventType,
                Join(data.LegRole, data.State, data.HangupCause ?? data.ProviderHangupCause, data.Reason, data.Target),
                startUtc,
                endUtc,
                interactionId,
                endUtc.HasValue ? data.DurationSeconds : null));
        }

        return entries
            .OrderBy(entry => entry.StartUtc)
            .ThenBy(entry => entry.RecordedUtc)
            .ThenBy(entry => entry.Kind)
            .ToArray();
    }

    private static void AddStates(List<AgentActivityEntry> entries, AgentStateTimeline timeline, DateTime fromUtc, DateTime toUtc)
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

            entries.Add(new AgentActivityEntry
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
            });
        }

        // A change that happened after a sign-off dated by the last heartbeat is shown, because it was recorded, but
        // it takes no time: the agent was already gone.
        foreach (var transition in timeline.Transitions.Where(transition => transition.Superseded && transition.ChangedUtc >= fromUtc && transition.ChangedUtc <= toUtc))
        {
            entries.Add(new AgentActivityEntry
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
