using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// One agent's state changes, in the order they took effect, with the ones that do not count marked.
/// </summary>
internal sealed class AgentStateTimeline
{
    /// <summary>
    /// The events an agent's state is read from: the state audit, and the older presence events for periods the
    /// audit does not cover.
    /// </summary>
    public static readonly string[] StateEventTypes =
    [
        ContactCenterConstants.Events.AgentStateChanged,
        ContactCenterConstants.Events.AgentSignedIn,
        ContactCenterConstants.Events.AgentSignedOut,
        ContactCenterConstants.Events.AgentPresenceChanged,
    ];

    private AgentStateTimeline(string agentId, IReadOnlyList<AgentStateTransition> transitions, DateTime? auditSinceUtc)
    {
        AgentId = agentId;
        Transitions = transitions;
        Effective = transitions.Where(transition => !transition.Superseded).ToArray();
        AuditSinceUtc = auditSinceUtc;
    }

    /// <summary>
    /// Gets the agent profile identifier.
    /// </summary>
    public string AgentId { get; }

    /// <summary>
    /// Gets every change read for the agent, in the order they took effect, including superseded ones.
    /// </summary>
    public IReadOnlyList<AgentStateTransition> Transitions { get; }

    /// <summary>
    /// Gets the changes that count, in the order they took effect.
    /// </summary>
    public IReadOnlyList<AgentStateTransition> Effective { get; }

    /// <summary>
    /// Gets when the state audit first covers the agent. The older presence events are read only before it.
    /// </summary>
    public DateTime? AuditSinceUtc { get; }

    /// <summary>
    /// Builds a timeline per agent from the agents' state events.
    /// </summary>
    /// <param name="events">The events: typically each agent's last state change before the period, and the period's.</param>
    /// <returns>One timeline per agent that has at least one state change.</returns>
    /// <remarks>
    /// The state audit records every transition, including the ones the older presence events never did
    /// (reservations, calls, releases and wrap-up), and it is written alongside them. So once the audit covers an
    /// agent, it is the whole account and the older events are ignored; before that, the older events are all there
    /// is. A sign-off dated by the agent's last heartbeat supersedes whatever was recorded for them between that
    /// heartbeat and the moment the platform noticed they were gone.
    /// </remarks>
    public static IReadOnlyList<AgentStateTimeline> Build(IEnumerable<InteractionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var timelines = new List<AgentStateTimeline>();

        foreach (var agentEvents in events
            .Where(interactionEvent => interactionEvent is not null && !string.IsNullOrEmpty(interactionEvent.AggregateId))
            .GroupBy(interactionEvent => interactionEvent.AggregateId, StringComparer.Ordinal))
        {
            var transitions = agentEvents
                .Select(Read)
                .Where(transition => transition is not null)
                .ToList();

            if (transitions.Count == 0)
            {
                continue;
            }

            var auditSinceUtc = transitions
                .Where(transition => transition.FromAudit)
                .Select(transition => (DateTime?)transition.ChangedUtc)
                .Min();

            if (auditSinceUtc.HasValue)
            {
                transitions.RemoveAll(transition => !transition.FromAudit && transition.ChangedUtc >= auditSinceUtc.Value);
            }

            var ordered = transitions
                .OrderBy(transition => transition.ChangedUtc)
                .ThenBy(transition => transition.RecordedUtc)
                .ThenBy(transition => transition.EventId, StringComparer.Ordinal)
                .ToArray();

            MarkSuperseded(ordered);
            MarkMissingTransitions(ordered);

            timelines.Add(new AgentStateTimeline(agentEvents.Key, ordered, auditSinceUtc));
        }

        return timelines;
    }

    /// <summary>
    /// Builds the state intervals of every agent from their state events, clipped to a period.
    /// </summary>
    /// <param name="events">The state events.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period: an agent still in a state then is counted up to it.</param>
    /// <returns>The intervals, each agent's in order.</returns>
    public static IReadOnlyList<AgentPresenceInterval> BuildIntervals(IEnumerable<InteractionEvent> events, DateTime fromUtc, DateTime toUtc)
        => BuildIntervals(Build(events), fromUtc, toUtc);

    /// <summary>
    /// Builds the state intervals of the supplied timelines, clipped to a period.
    /// </summary>
    /// <param name="timelines">The timelines.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period: an agent still in a state then is counted up to it.</param>
    /// <returns>The intervals, each agent's in order.</returns>
    public static IReadOnlyList<AgentPresenceInterval> BuildIntervals(IEnumerable<AgentStateTimeline> timelines, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(timelines);

        var intervals = new List<AgentPresenceInterval>();

        foreach (var timeline in timelines)
        {
            var effective = timeline.Effective;

            for (var index = 0; index < effective.Count; index++)
            {
                var transition = effective[index];
                var startUtc = transition.ChangedUtc;
                var endUtc = index + 1 < effective.Count ? effective[index + 1].ChangedUtc : toUtc;
                var clippedStart = startUtc < fromUtc ? fromUtc : startUtc;
                var clippedEnd = endUtc > toUtc ? toUtc : endUtc;

                if (clippedEnd <= clippedStart)
                {
                    continue;
                }

                intervals.Add(new AgentPresenceInterval
                {
                    AgentId = timeline.AgentId,
                    Status = transition.CurrentState,
                    Reason = transition.Reason,
                    QueueIds = [.. transition.QueueIds],
                    CampaignIds = [.. transition.CampaignIds],
                    StartUtc = clippedStart,
                    EndUtc = clippedEnd,
                    FromAudit = transition.FromAudit,
                });
            }
        }

        return intervals;
    }

    /// <summary>
    /// Builds the spans the agent was signed in, from each sign-in to the next sign-off, clipped to a period.
    /// </summary>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period: an agent still signed in then is counted up to it.</param>
    /// <returns>The signed-in spans, in order.</returns>
    /// <remarks>
    /// This is measured from sign-ins and sign-offs alone, independently of the states in between, which is what lets
    /// a timecard check that its states add up to it. An agent already in a signed-in state when the period opened is
    /// signed in from its start.
    /// </remarks>
    public IReadOnlyList<AgentSignedInSpan> BuildSignedInSpans(DateTime fromUtc, DateTime toUtc)
    {
        var spans = new List<AgentSignedInSpan>();
        DateTime? signedInUtc = null;

        for (var index = 0; index < Effective.Count; index++)
        {
            var transition = Effective[index];

            if (transition.ChangedUtc > toUtc)
            {
                break;
            }

            if (signedInUtc is null)
            {
                var alreadySignedIn = index == 0 && transition.ChangedUtc < fromUtc && !transition.IsSignOff;

                if (transition.IsSignIn || alreadySignedIn)
                {
                    signedInUtc = transition.ChangedUtc;
                }
            }
            else if (transition.IsSignOff)
            {
                Add(spans, signedInUtc.Value, transition.ChangedUtc, signedOff: true, fromUtc, toUtc);
                signedInUtc = null;
            }
        }

        if (signedInUtc.HasValue)
        {
            Add(spans, signedInUtc.Value, toUtc, signedOff: false, fromUtc, toUtc);
        }

        return spans;
    }

    private void Add(List<AgentSignedInSpan> spans, DateTime startUtc, DateTime endUtc, bool signedOff, DateTime fromUtc, DateTime toUtc)
    {
        var clippedStart = startUtc < fromUtc ? fromUtc : startUtc;
        var clippedEnd = endUtc > toUtc ? toUtc : endUtc;

        if (clippedEnd > clippedStart)
        {
            spans.Add(new AgentSignedInSpan(AgentId, clippedStart, clippedEnd, signedOff && clippedEnd == endUtc));
        }
    }

    // Everything recorded between a sign-off's effective time and the moment it was recorded happened while the agent
    // was already gone: a sweep dates the sign-off by the last heartbeat, and routing may have moved the agent in the
    // meantime. A sign-in is never superseded, because it is the agent coming back.
    private static void MarkSuperseded(AgentStateTransition[] ordered)
    {
        AgentStateTransition signOff = null;

        foreach (var transition in ordered)
        {
            if (signOff is not null)
            {
                if (!transition.IsSignIn && transition.RecordedUtc < signOff.RecordedUtc)
                {
                    transition.Superseded = true;

                    continue;
                }

                signOff = null;
            }

            if (transition.IsSignOff)
            {
                signOff = transition;
            }
        }
    }

    // A change names the state it left as the state was when it was recorded, so the chain is checked in the order the
    // changes were recorded. A change that did not leave the state the one before it entered means a transition in
    // between is missing from the record.
    private static void MarkMissingTransitions(AgentStateTransition[] ordered)
    {
        AgentStateTransition prior = null;

        foreach (var transition in ordered
            .OrderBy(transition => transition.RecordedUtc)
            .ThenBy(transition => transition.ChangedUtc)
            .ThenBy(transition => transition.EventId, StringComparer.Ordinal))
        {
            if (prior is not null && transition.PreviousState != prior.CurrentState)
            {
                transition.ExpectedPreviousState = prior.CurrentState;
            }

            prior = transition;
        }
    }

    private static AgentStateTransition Read(InteractionEvent interactionEvent)
    {
        var recordedUtc = interactionEvent.RecordedUtc == default ? interactionEvent.OccurredUtc : interactionEvent.RecordedUtc;

        if (string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.AgentStateChanged, StringComparison.Ordinal))
        {
            var change = interactionEvent.GetData<AgentStateChangedEventData>();

            if (change is null)
            {
                return null;
            }

            var changedUtc = change.ChangedUtc == default ? interactionEvent.OccurredUtc : Utc(change.ChangedUtc);

            return new AgentStateTransition
            {
                AgentId = interactionEvent.AggregateId,
                EventId = interactionEvent.ItemId,
                EventType = interactionEvent.EventType,
                FromAudit = true,
                ChangedUtc = changedUtc,
                RecordedUtc = recordedUtc < changedUtc ? changedUtc : recordedUtc,
                PreviousState = change.PreviousState,
                CurrentState = change.CurrentState,
                RequestedState = change.RequestedState,
                ReasonCodeId = change.ReasonCodeId,
                Reason = change.ReasonName,
                Source = change.Source,
                InteractionId = change.InteractionId,
                ReservationId = change.ReservationId,
                QueueIds = [.. change.QueueIds ?? []],
                CampaignIds = [.. change.CampaignIds ?? []],
                ActorType = interactionEvent.ActorType,
                ActorId = interactionEvent.ActorId,
            };
        }

        if (!string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.AgentSignedIn, StringComparison.Ordinal) &&
            !string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.AgentSignedOut, StringComparison.Ordinal) &&
            !string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.AgentPresenceChanged, StringComparison.Ordinal))
        {
            return null;
        }

        var presence = interactionEvent.GetData<AgentPresenceChangedEventData>();

        if (presence is null)
        {
            return null;
        }

        var presenceChangedUtc = presence.ChangedUtc == default ? interactionEvent.OccurredUtc : Utc(presence.ChangedUtc);

        return new AgentStateTransition
        {
            AgentId = interactionEvent.AggregateId,
            EventId = interactionEvent.ItemId,
            EventType = interactionEvent.EventType,
            FromAudit = false,
            ChangedUtc = presenceChangedUtc,
            RecordedUtc = recordedUtc < presenceChangedUtc ? presenceChangedUtc : recordedUtc,
            PreviousState = presence.PreviousStatus,
            CurrentState = string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.AgentSignedOut, StringComparison.Ordinal)
                ? AgentPresenceStatus.Offline
                : presence.CurrentStatus,
            RequestedState = presence.RequestedStatus,
            Reason = presence.Reason,
            QueueIds = [.. presence.QueueIds ?? []],
            CampaignIds = [.. presence.CampaignIds ?? []],
            ActorType = interactionEvent.ActorType,
            ActorId = interactionEvent.ActorId,
        };
    }

    private static DateTime Utc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

/// <summary>
/// A span an agent was signed in.
/// </summary>
/// <param name="AgentId">The agent profile identifier.</param>
/// <param name="StartUtc">When the span starts within the period.</param>
/// <param name="EndUtc">When the span ends within the period.</param>
/// <param name="EndedBySignOff">Whether the span ends because the agent signed off, rather than at the end of the period.</param>
internal readonly record struct AgentSignedInSpan(string AgentId, DateTime StartUtc, DateTime EndUtc, bool EndedBySignOff)
{
    /// <summary>
    /// Gets the span's length in seconds.
    /// </summary>
    public double DurationSeconds => (EndUtc - StartUtc).TotalSeconds;
}
