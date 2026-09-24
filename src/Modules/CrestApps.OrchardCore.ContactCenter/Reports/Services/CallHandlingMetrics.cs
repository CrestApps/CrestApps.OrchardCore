using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Call handling as the event log records it: talk time less hold, ring time, the real wait in queue, and abandons.
/// </summary>
internal sealed class CallHandlingMetrics
{
    /// <summary>
    /// The call events the metrics are measured from.
    /// </summary>
    public static readonly string[] CallEventTypes =
    [
        ContactCenterConstants.Events.CallQueued,
        ContactCenterConstants.Events.CallDequeued,
        ContactCenterConstants.Events.CallAbandoned,
        ContactCenterConstants.Events.AgentLegAnswered,
        ContactCenterConstants.Events.CallConnected,
        ContactCenterConstants.Events.AgentLegFailed,
        ContactCenterConstants.Events.ConsultCompleted,
        ContactCenterConstants.Events.CallHeld,
        ContactCenterConstants.Events.CallResumed,
        ContactCenterConstants.Events.CallEnded,
    ];

    /// <summary>
    /// The offer events ring time is measured from.
    /// </summary>
    public static readonly string[] OfferEventTypes =
    [
        ContactCenterConstants.Events.OfferPresented,
        ContactCenterConstants.Events.OfferAccepted,
        ContactCenterConstants.Events.OfferDeclined,
        ContactCenterConstants.Events.OfferExpired,
        ContactCenterConstants.Events.OfferMissed,
        ContactCenterConstants.Events.OfferCancelled,
    ];

    /// <summary>
    /// Gets the metrics per agent.
    /// </summary>
    public IReadOnlyList<CallHandlingAgentMetrics> Agents { get; private init; } = [];

    /// <summary>
    /// Gets the metrics per queue.
    /// </summary>
    public IReadOnlyList<CallHandlingQueueMetrics> Queues { get; private init; } = [];

    /// <summary>
    /// Gets how many calls were abandoned, including those that never reached a queue.
    /// </summary>
    public int Abandoned { get; private init; }

    /// <summary>
    /// Measures call handling from the period's call and offer events.
    /// </summary>
    /// <param name="callEvents">The period's call events.</param>
    /// <param name="offerEvents">The period's offer events.</param>
    /// <param name="toUtc">The end of the period: a call still connected then is counted up to it.</param>
    /// <param name="agentId">The agent to limit the metrics to, or <see langword="null"/>.</param>
    /// <param name="queueId">The queue to limit the metrics to, or <see langword="null"/>.</param>
    /// <returns>The metrics.</returns>
    public static CallHandlingMetrics Calculate(
        IEnumerable<InteractionEvent> callEvents,
        IEnumerable<InteractionEvent> offerEvents,
        DateTime toUtc,
        string agentId = null,
        string queueId = null)
    {
        ArgumentNullException.ThrowIfNull(callEvents);
        ArgumentNullException.ThrowIfNull(offerEvents);

        var agents = new Dictionary<string, CallHandlingAgentMetrics>(StringComparer.Ordinal);
        var queues = new Dictionary<string, CallHandlingQueueMetrics>(StringComparer.Ordinal);
        var abandoned = 0;

        CallHandlingAgentMetrics Agent(string id)
        {
            if (!agents.TryGetValue(id, out var metrics))
            {
                agents[id] = metrics = new CallHandlingAgentMetrics { AgentId = id };
            }

            return metrics;
        }

        CallHandlingQueueMetrics Queue(string id)
        {
            id ??= string.Empty;

            if (!queues.TryGetValue(id, out var metrics))
            {
                queues[id] = metrics = new CallHandlingQueueMetrics { QueueId = id };
            }

            return metrics;
        }

        foreach (var call in ReadCalls(callEvents))
        {
            var calls = call.ToArray();
            var callQueueId = calls.Select(entry => entry.Data.QueueId).FirstOrDefault(id => !string.IsNullOrEmpty(id));

            if (!string.IsNullOrEmpty(queueId) && !string.Equals(callQueueId, queueId, StringComparison.Ordinal))
            {
                continue;
            }

            var abandon = calls.FirstOrDefault(entry => entry.Is(ContactCenterConstants.Events.CallAbandoned));

            if (string.IsNullOrEmpty(agentId))
            {
                abandoned += abandon is null ? 0 : 1;
                MeasureQueues(calls, callQueueId, abandon, Queue);
            }

            foreach (var agentCalls in calls
                .Where(entry => !string.IsNullOrEmpty(entry.Data.AgentId) && entry.IsAnswer)
                .Select(entry => entry.Data.AgentId)
                .Distinct(StringComparer.Ordinal))
            {
                if (!string.IsNullOrEmpty(agentId) && !string.Equals(agentCalls, agentId, StringComparison.Ordinal))
                {
                    continue;
                }

                MeasureAgent(calls, agentCalls, toUtc, Agent(agentCalls));
            }
        }

        foreach (var offer in ReadOffers(offerEvents))
        {
            var first = offer.First();

            if ((!string.IsNullOrEmpty(agentId) && !string.Equals(first.Data.AgentId, agentId, StringComparison.Ordinal)) ||
                (!string.IsNullOrEmpty(queueId) && !string.Equals(first.Data.QueueId, queueId, StringComparison.Ordinal)) ||
                string.IsNullOrEmpty(first.Data.AgentId))
            {
                continue;
            }

            var metrics = Agent(first.Data.AgentId);
            var settled = offer.LastOrDefault(entry => !string.Equals(entry.Event.EventType, ContactCenterConstants.Events.OfferPresented, StringComparison.Ordinal));

            metrics.OffersPresented++;

            if (settled is null)
            {
                continue;
            }

            var ringSeconds = settled.Data.RingSeconds ??
                (settled.Data.PresentedUtc.HasValue && settled.Data.SettledUtc.HasValue
                    ? Math.Max(0, (settled.Data.SettledUtc.Value - settled.Data.PresentedUtc.Value).TotalSeconds)
                    : 0);

            metrics.RingSeconds += ringSeconds;
            metrics.OffersSettled++;

            switch (settled.Event.EventType)
            {
                case ContactCenterConstants.Events.OfferAccepted:
                    metrics.OffersAccepted++;
                    metrics.RingToAnswerSeconds += ringSeconds;
                    break;
                case ContactCenterConstants.Events.OfferDeclined:
                    metrics.OffersDeclined++;
                    break;
                case ContactCenterConstants.Events.OfferExpired:
                case ContactCenterConstants.Events.OfferMissed:
                    metrics.OffersMissed++;
                    break;
                default:
                    metrics.OffersCancelled++;
                    break;
            }
        }

        return new CallHandlingMetrics
        {
            Agents = [.. agents.Values],
            Queues = [.. queues.Values.Where(queue => queue.Queued > 0 || queue.Abandoned > 0 || queue.AnsweredFromQueue > 0)],
            Abandoned = abandoned,
        };
    }

    private static void MeasureQueues(
        CallEntry[] calls,
        string callQueueId,
        CallEntry abandon,
        Func<string, CallHandlingQueueMetrics> queue)
    {
        var queuedAt = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        foreach (var entry in calls)
        {
            var entryQueueId = string.IsNullOrEmpty(entry.Data.QueueId) ? callQueueId ?? string.Empty : entry.Data.QueueId;

            if (entry.Is(ContactCenterConstants.Events.CallQueued))
            {
                if (queuedAt.TryAdd(entryQueueId, entry.OccurredUtc))
                {
                    queue(entryQueueId).Queued++;
                }
            }
            else if (entry.Is(ContactCenterConstants.Events.CallDequeued) && abandon is null)
            {
                // The wait the queue itself measured, falling back to the time since the call joined it.
                var wait = entry.Data.DurationSeconds ??
                    (queuedAt.TryGetValue(entryQueueId, out var joinedUtc) ? Math.Max(0, (entry.OccurredUtc - joinedUtc).TotalSeconds) : 0);
                var metrics = queue(entryQueueId);

                metrics.AnsweredFromQueue++;
                metrics.AnsweredWaitSeconds += wait;
                metrics.LongestWaitSeconds = Math.Max(metrics.LongestWaitSeconds, wait);
            }
        }

        if (abandon is null)
        {
            return;
        }

        // A caller who hangs up waiting is an abandon, never an answered call, whatever else the call went through.
        var abandonQueueId = string.IsNullOrEmpty(abandon.Data.QueueId) ? callQueueId ?? string.Empty : abandon.Data.QueueId;
        var abandonMetrics = queue(abandonQueueId);
        var abandonWait = abandon.Data.DurationSeconds ??
            (queuedAt.TryGetValue(abandonQueueId, out var queuedUtc) ? Math.Max(0, (abandon.OccurredUtc - queuedUtc).TotalSeconds) : 0);

        abandonMetrics.Abandoned++;
        abandonMetrics.AbandonedWaitSeconds += abandonWait;
        abandonMetrics.LongestWaitSeconds = Math.Max(abandonMetrics.LongestWaitSeconds, abandonWait);
    }

    private static void MeasureAgent(CallEntry[] calls, string agentId, DateTime toUtc, CallHandlingAgentMetrics metrics)
    {
        DateTime? connectedUtc = null;
        DateTime? heldUtc = null;
        var handled = false;

        foreach (var entry in calls)
        {
            var forAgent = string.IsNullOrEmpty(entry.Data.AgentId) || string.Equals(entry.Data.AgentId, agentId, StringComparison.Ordinal);

            if (!forAgent)
            {
                continue;
            }

            if (connectedUtc is null)
            {
                if (entry.IsAnswer && string.Equals(entry.Data.AgentId, agentId, StringComparison.Ordinal))
                {
                    connectedUtc = entry.OccurredUtc;
                    handled = true;
                }

                continue;
            }

            if (entry.Is(ContactCenterConstants.Events.CallHeld))
            {
                if (heldUtc is null)
                {
                    heldUtc = entry.OccurredUtc;
                    metrics.Holds++;
                }
            }
            else if (entry.Is(ContactCenterConstants.Events.CallResumed))
            {
                // The hold the resume measured, falling back to the time since the hold began.
                if (heldUtc is null && entry.Data.DurationSeconds.HasValue)
                {
                    metrics.Holds++;
                }

                metrics.HeldSeconds += entry.Data.DurationSeconds ??
                    (heldUtc.HasValue ? Math.Max(0, (entry.OccurredUtc - heldUtc.Value).TotalSeconds) : 0);
                heldUtc = null;
            }
            else if (entry.IsAgentEnd(agentId))
            {
                Close(metrics, connectedUtc.Value, entry.OccurredUtc, heldUtc);
                connectedUtc = null;
                heldUtc = null;
            }
        }

        if (connectedUtc.HasValue && connectedUtc.Value < toUtc)
        {
            Close(metrics, connectedUtc.Value, toUtc, heldUtc);
        }

        if (handled)
        {
            metrics.CallsHandled++;
        }
    }

    private static void Close(CallHandlingAgentMetrics metrics, DateTime connectedUtc, DateTime endedUtc, DateTime? heldUtc)
    {
        if (heldUtc.HasValue)
        {
            metrics.HeldSeconds += Math.Max(0, (endedUtc - heldUtc.Value).TotalSeconds);
        }

        metrics.ConnectedSeconds += Math.Max(0, (endedUtc - connectedUtc).TotalSeconds);

        // Hold is part of the connected time, so it can never be more than it.
        metrics.HeldSeconds = Math.Min(metrics.HeldSeconds, metrics.ConnectedSeconds);
    }

    private static IEnumerable<IGrouping<string, CallEntry>> ReadCalls(IEnumerable<InteractionEvent> events)
        => events
            .Where(interactionEvent => interactionEvent is not null)
            .Select(interactionEvent => new CallEntry(interactionEvent, interactionEvent.GetData<CallLifecycleEventData>()))
            .Where(entry => entry.Data is not null)
            .OrderBy(entry => entry.OccurredUtc)
            .ThenBy(entry => entry.Event.RecordedUtc)
            .GroupBy(entry => entry.Data.InteractionId ?? entry.Event.InteractionId ?? entry.Event.AggregateId, StringComparer.Ordinal);

    private static IEnumerable<IGrouping<string, OfferEntry>> ReadOffers(IEnumerable<InteractionEvent> events)
        => events
            .Where(interactionEvent => interactionEvent is not null)
            .Select(interactionEvent => new OfferEntry(interactionEvent, interactionEvent.GetData<OfferLifecycleEventData>()))
            .Where(entry => entry.Data is not null)
            .OrderBy(entry => entry.Event.OccurredUtc)
            .GroupBy(entry => entry.Data.ReservationId ?? entry.Event.AggregateId, StringComparer.Ordinal);

    private sealed record CallEntry(InteractionEvent Event, CallLifecycleEventData Data)
    {
        public DateTime OccurredUtc => Event.OccurredUtc;

        public bool IsAnswer
            => Is(ContactCenterConstants.Events.AgentLegAnswered) || Is(ContactCenterConstants.Events.CallConnected);

        public bool Is(string eventType)
            => string.Equals(Event.EventType, eventType, StringComparison.Ordinal);

        // The agent's part of a call ends when the call does, when their own leg drops, or when they hand the caller on.
        public bool IsAgentEnd(string agentId)
            => Is(ContactCenterConstants.Events.CallEnded) ||
                ((Is(ContactCenterConstants.Events.AgentLegFailed) || Is(ContactCenterConstants.Events.ConsultCompleted)) &&
                    string.Equals(Data.AgentId, agentId, StringComparison.Ordinal));
    }

    private sealed record OfferEntry(InteractionEvent Event, OfferLifecycleEventData Data);
}

/// <summary>
/// One agent's call handling.
/// </summary>
internal sealed class CallHandlingAgentMetrics
{
    public string AgentId { get; init; }

    public int CallsHandled { get; set; }

    public double ConnectedSeconds { get; set; }

    public double HeldSeconds { get; set; }

    public int Holds { get; set; }

    public double TalkSeconds => Math.Max(0, ConnectedSeconds - HeldSeconds);

    public int OffersPresented { get; set; }

    public int OffersSettled { get; set; }

    public int OffersAccepted { get; set; }

    public int OffersDeclined { get; set; }

    public int OffersMissed { get; set; }

    public int OffersCancelled { get; set; }

    public double RingSeconds { get; set; }

    public double RingToAnswerSeconds { get; set; }
}

/// <summary>
/// One queue's waits and abandons.
/// </summary>
internal sealed class CallHandlingQueueMetrics
{
    public string QueueId { get; init; }

    public int Queued { get; set; }

    public int AnsweredFromQueue { get; set; }

    public int Abandoned { get; set; }

    public double AnsweredWaitSeconds { get; set; }

    public double AbandonedWaitSeconds { get; set; }

    public double LongestWaitSeconds { get; set; }

    public double AbandonRate
    {
        get
        {
            var offered = Math.Max(Queued, AnsweredFromQueue + Abandoned);

            return offered > 0 ? (double)Abandoned / offered : 0;
        }
    }
}
