using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// A call or activity an agent holds that is not finished.
/// </summary>
/// <param name="InteractionId">The interaction's identifier.</param>
/// <param name="ActivityId">The activity the interaction is for.</param>
/// <param name="Status">The interaction's status.</param>
/// <param name="StartedUtc">When it started.</param>
/// <param name="AwaitingWrapUp">Whether the call is over and the agent has not finished the after-call work.</param>
internal sealed record AgentOpenWork(string InteractionId, string ActivityId, string Status, DateTime? StartedUtc, bool AwaitingWrapUp);

/// <summary>
/// A reservation that holds work for an agent.
/// </summary>
/// <param name="ReservationId">The reservation's identifier.</param>
/// <param name="ActivityId">The activity reserved.</param>
/// <param name="QueueId">The queue it came from.</param>
/// <param name="Status">The reservation's status.</param>
/// <param name="CreatedUtc">When it was made.</param>
/// <param name="ExpiresUtc">When it expires.</param>
internal sealed record AgentReservation(string ReservationId, string ActivityId, string QueueId, string Status, DateTime CreatedUtc, DateTime ExpiresUtc);

/// <summary>
/// What became of one offer made to an agent.
/// </summary>
/// <param name="EventType">What happened: presented, accepted, declined, expired, missed or cancelled.</param>
/// <param name="ReservationId">The reservation the offer was made under.</param>
/// <param name="ActivityId">The activity offered.</param>
/// <param name="QueueId">The queue it came from.</param>
/// <param name="RingSeconds">How long the offer rang before it was settled.</param>
/// <param name="Reason">Why it ended the way it did, when it was not accepted.</param>
/// <param name="OccurredUtc">When it happened.</param>
internal sealed record AgentOffer(string EventType, string ReservationId, string ActivityId, string QueueId, double? RingSeconds, string Reason, DateTime OccurredUtc);

/// <summary>
/// One routing decision or dial suppression that concerned an agent, with how often it was repeated.
/// </summary>
/// <param name="Kind">Routing or Suppression.</param>
/// <param name="Reason">The decision's reason.</param>
/// <param name="ActivityId">The activity concerned, when one was.</param>
/// <param name="QueueId">The queue concerned.</param>
/// <param name="Count">How many times the decision was made.</param>
/// <param name="LastSeenUtc">When it was last made.</param>
internal sealed record AgentDecision(string Kind, string Reason, string ActivityId, string QueueId, int Count, DateTime LastSeenUtc);

/// <summary>
/// Everything read about one agent, which <see cref="AgentCallAnalyzer"/> turns into findings.
/// </summary>
/// <param name="Diagnosis">What the agent availability reader found.</param>
/// <param name="OpenWork">The calls and activities the agent holds that are not finished.</param>
/// <param name="Reservations">The reservations that hold work for the agent now.</param>
/// <param name="Offers">What became of the offers made to the agent in the period.</param>
/// <param name="Decisions">The routing decisions and dial suppressions that concerned the agent in the period.</param>
/// <param name="NowUtc">The time the agent was read.</param>
/// <param name="Lookback">How far back the offers and decisions reach.</param>
/// <param name="QueueFilter">The queue or campaign the question was about, or <see langword="null"/> for all of them.</param>
/// <param name="VisibleQueueIds">The queues and campaign queues the person asking oversees, or <see langword="null"/> when
/// every queue may be shown. A queue they do not oversee is left out of the findings.</param>
internal sealed record AgentCallFacts(
    AgentAvailabilityDiagnosis Diagnosis,
    IReadOnlyList<AgentOpenWork> OpenWork,
    IReadOnlyList<AgentReservation> Reservations,
    IReadOnlyList<AgentOffer> Offers,
    IReadOnlyList<AgentDecision> Decisions,
    DateTime NowUtc,
    TimeSpan Lookback,
    string QueueFilter,
    IReadOnlySet<string> VisibleQueueIds);

/// <summary>
/// What the analyzer concluded.
/// </summary>
/// <param name="Verdict">The answer, in a sentence or two.</param>
/// <param name="Findings">The findings, most likely first.</param>
internal sealed record AgentCallAnalysis(string Verdict, IReadOnlyList<ToolFinding> Findings);

/// <summary>
/// Says why an agent is not receiving calls, from the facts read about them. It invents nothing: each finding names the
/// record it rests on, and an agent with no blocking finding is said to be eligible rather than guessed about.
/// </summary>
internal static class AgentCallAnalyzer
{
    /// <summary>
    /// Analyzes one agent.
    /// </summary>
    /// <param name="facts">What was read about the agent.</param>
    /// <returns>The verdict and findings.</returns>
    public static AgentCallAnalysis Analyze(AgentCallFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var diagnosis = facts.Diagnosis;
        var agent = diagnosis.Agent;
        var name = string.IsNullOrWhiteSpace(agent.DisplayName) ? agent.UserName ?? agent.Name ?? agent.ItemId : agent.DisplayName;
        var findings = new List<ToolFinding>();
        IReadOnlyList<AgentQueueEligibility> visible = facts.VisibleQueueIds is null
            ? diagnosis.Queues
            : [.. diagnosis.Queues.Where(queue => facts.VisibleQueueIds.Contains(queue.QueueId))];
        IReadOnlyList<AgentQueueEligibility> queues = facts.QueueFilter is null
            ? visible
            : [.. visible.Where(queue => Matches(queue, facts.QueueFilter))];

        if (!diagnosis.SignedInAnywhere)
        {
            var entitled = visible.Select(queue => queue.Name).ToArray();

            findings.Add(new ToolFinding(
                "NotSignedIn",
                Likelihood.Certain,
                entitled.Length == 0
                    ? $"{name} is not signed in to any queue or campaign and is not entitled to any, so routing has nothing to give them."
                    : $"{name} is not signed in to any queue or campaign. They can sign in to: {string.Join(", ", entitled)}.",
                new { signedInTo = Array.Empty<string>(), canSignInTo = entitled }));
        }
        else if (facts.QueueFilter is not null && queues.Count > 0 && queues.All(queue => !queue.SignedIn))
        {
            findings.Add(new ToolFinding(
                "NotSignedInToQueue",
                Likelihood.Certain,
                $"{name} is not signed in to {string.Join(", ", queues.Select(queue => queue.Name))}. They are signed in to: {Describe(diagnosis)}.",
                new { signedInQueues = diagnosis.SignedInQueues, signedInCampaigns = diagnosis.SignedInCampaigns }));
        }

        if (facts.QueueFilter is not null && queues.Count == 0)
        {
            findings.Add(new ToolFinding(
                "NotEntitledToQueue",
                Likelihood.Certain,
                $"{name} is neither entitled to nor signed in to anything matching '{facts.QueueFilter}'. They are entitled to or signed in to: {string.Join(", ", visible.Select(queue => queue.Name))}.",
                null));
        }

        if (!diagnosis.SessionLive)
        {
            findings.Add(new ToolFinding(
                "NoLiveSession",
                Likelihood.Certain,
                !diagnosis.HasSession
                    ? $"{name} has no live session: they are not signed in to the agent workspace in a browser, so routing passes them over."
                    : $"{name}'s session is offline, disconnected or has no recent heartbeat (last heartbeat {Format(diagnosis.LastHeartbeatUtc)}), so routing passes them over.",
                new { hasSession = diagnosis.HasSession, lastHeartbeatUtc = Format(diagnosis.LastHeartbeatUtc) }));
        }

        if (agent.PresenceStatus != AgentPresenceStatus.Available)
        {
            var reason = string.IsNullOrWhiteSpace(agent.PresenceReason) ? string.Empty : $" ({agent.PresenceReason})";

            findings.Add(new ToolFinding(
                "NotAvailable",
                Likelihood.Certain,
                $"{name}'s presence is {agent.PresenceStatus}{reason}{(agent.PresenceChangedUtc.HasValue ? $" since {Format(agent.PresenceChangedUtc)}" : string.Empty)}. Routing only offers work to agents who are Available.",
                new { presence = agent.PresenceStatus.ToString(), reason = agent.PresenceReason, sinceUtc = Format(agent.PresenceChangedUtc) }));
        }

        AddHeldWork(findings, facts, name);
        AddStaleState(findings, diagnosis, name);
        AddQueueFindings(findings, facts, queues, name);
        AddOfferFindings(findings, facts, name);
        AddDecisionFindings(findings, facts);

        var ordered = ToolFinding.Order(findings);

        return new AgentCallAnalysis(BuildVerdict(name, queues, ordered), ordered);
    }

    private static void AddHeldWork(List<ToolFinding> findings, AgentCallFacts facts, string name)
    {
        var diagnosis = facts.Diagnosis;

        foreach (var reservation in facts.Reservations)
        {
            var expired = reservation.ExpiresUtc <= facts.NowUtc;

            findings.Add(new ToolFinding(
                expired ? "ExpiredReservationStillHeld" : "HoldsReservation",
                Likelihood.Certain,
                expired
                    ? $"{name} still holds reservation {reservation.ReservationId} for activity {reservation.ActivityId}, which expired at {Format(reservation.ExpiresUtc)} and has not been released. Nothing else is offered to them until it is released."
                    : $"{name} holds reservation {reservation.ReservationId} for activity {reservation.ActivityId}, offered at {Format(reservation.CreatedUtc)} and expiring at {Format(reservation.ExpiresUtc)}. No further work is offered while an offer is open.",
                reservation));
        }

        if (facts.OpenWork.Count > 0)
        {
            var wrapUps = facts.OpenWork.Count(work => work.AwaitingWrapUp);
            var capacity = diagnosis.Queues.Count > 0 ? diagnosis.Queues.Max(queue => queue.Capacity) : Math.Max(1, diagnosis.Agent.MaxConcurrentInteractions);
            var activities = string.Join(", ", facts.OpenWork.Select(work => $"{work.ActivityId} ({(work.AwaitingWrapUp ? "after-call work not finished" : work.Status)})"));

            findings.Add(new ToolFinding(
                "UnfinishedActivity",
                Likelihood.Certain,
                $"{name} holds {facts.OpenWork.Count} unfinished item(s): {activities}. They can handle {capacity} at once, so routing offers them nothing more until the item is finished and dispositioned." +
                    (wrapUps > 0 ? " The call ended but the after-call work was never completed." : string.Empty),
                new { capacity, open = facts.OpenWork }));
        }
    }

    private static void AddStaleState(List<ToolFinding> findings, AgentAvailabilityDiagnosis diagnosis, string name)
    {
        foreach (var flag in diagnosis.Flags.Where(flag => flag.Kind is not (AgentAvailabilityFlagKind.HoldsReservation or AgentAvailabilityFlagKind.StaleWorkNotChecked or AgentAvailabilityFlagKind.AvailableWithoutLiveSession)))
        {
            findings.Add(new ToolFinding(
                $"Stale.{flag.Kind}",
                Likelihood.Likely,
                $"{name} has a stale state: {Words(flag.Kind)}{(flag.SinceUtc.HasValue ? $" since {Format(flag.SinceUtc)}" : string.Empty)}{(string.IsNullOrWhiteSpace(flag.Detail) ? string.Empty : $" ({flag.Detail})")}. Nothing live backs it, so it blocks work until it is released.",
                new { kind = flag.Kind.ToString(), flag.Detail, sinceUtc = Format(flag.SinceUtc) }));
        }
    }

    private static void AddQueueFindings(List<ToolFinding> findings, AgentCallFacts facts, IReadOnlyList<AgentQueueEligibility> queues, string name)
    {
        foreach (var queue in queues)
        {
            var label = queue.IsCampaign ? $"campaign '{queue.Name}'" : $"queue '{queue.Name}'";

            if (queue.Blocker != AgentQueueBlocker.None)
            {
                findings.Add(new ToolFinding(
                    $"Queue.{queue.Blocker}",
                    Likelihood.Certain,
                    queue.Blocker switch
                    {
                        AgentQueueBlocker.Missing => $"The {label} no longer exists, so it hands nobody work.",
                        AgentQueueBlocker.Disabled => $"The {label} is turned off, so it hands nobody work.",
                        _ => $"The {label} is closed by its business hours, so it hands nobody work.",
                    },
                    new { queue.QueueId, blocker = queue.Blocker.ToString() }));

                continue;
            }

            if (!queue.Entitled && queue.SignedIn)
            {
                findings.Add(new ToolFinding(
                    "Queue.NotEntitled",
                    Likelihood.Certain,
                    $"{name} is signed in to {label} but no manager entitled them to it, so routing skips them there.",
                    new { queue.QueueId }));
            }

            if (!queue.SignedIn && queue.Entitled && facts.QueueFilter is null && facts.Diagnosis.SignedInAnywhere)
            {
                findings.Add(new ToolFinding(
                    "Queue.NotSignedIn",
                    Likelihood.Possible,
                    $"{name} is entitled to {label} but is not signed in to it, so it gives them no work.",
                    new { queue.QueueId, queue.WaitingCount }));
            }

            if (!queue.SignedIn)
            {
                continue;
            }

            if (queue.WaitingCount == 0)
            {
                findings.Add(new ToolFinding(
                    "Queue.NothingWaiting",
                    queue.Gate == AgentRoutingIneligibility.None ? Likelihood.Likely : Likelihood.Informational,
                    $"Nothing is waiting in {label}, so there is nothing to offer {name} from it. For a campaign, records that are held back, assigned to a user for manual work, scheduled for later or not in any queue are not waiting; use the queue inventory tool to see them.",
                    new { queue.QueueId, waiting = 0 }));

                continue;
            }

            if (queue.RoutingEvaluated && queue.RoutingEligible == false)
            {
                findings.Add(new ToolFinding(
                    "Queue.RoutingPassesOver",
                    Likelihood.Likely,
                    $"Routing passes {name} over for the next waiting item in {label}: {string.Join(" ", queue.RoutingReasons)}",
                    new { queue.QueueId, reasons = queue.RoutingReasons }));
            }
            else if (queue.Eligible && queue.NextInLine)
            {
                findings.Add(new ToolFinding(
                    "Queue.NextInLine",
                    Likelihood.Informational,
                    $"{name} is eligible and next in line for the {queue.WaitingCount} item(s) waiting in {label}.",
                    new { queue.QueueId, queue.WaitingCount }));
            }
            else if (queue.Eligible)
            {
                findings.Add(new ToolFinding(
                    "Queue.AnotherAgentAhead",
                    Likelihood.Informational,
                    $"{name} is eligible for {label}, which has {queue.WaitingCount} item(s) waiting, but another agent is ahead of them in line.",
                    new { queue.QueueId, queue.WaitingCount }));
            }
        }
    }

    private static void AddOfferFindings(List<ToolFinding> findings, AgentCallFacts facts, string name)
    {
        var expired = facts.Offers
            .Where(offer => offer.EventType is ContactCenterConstants.Events.OfferExpired or ContactCenterConstants.Events.OfferMissed)
            .OrderByDescending(offer => offer.OccurredUtc)
            .ToArray();

        if (expired.Length > 0)
        {
            var last = expired[0];
            var rang = last.RingSeconds.HasValue ? $" after ringing {Math.Round(last.RingSeconds.Value)} s" : string.Empty;

            findings.Add(new ToolFinding(
                "OfferExpired",
                Likelihood.Likely,
                $"{expired.Length} offer(s) to {name} rang out unanswered in the last {Hours(facts.Lookback)}. The most recent was for activity {last.ActivityId}, which expired{rang} at {Format(last.OccurredUtc)}. When an offer expires the agent loses the reservation and the record goes back to the queue, so the agent is no longer the owner." +
                    " Repeated expiry usually means the agent was away, the soft phone was not reachable or the offer timeout is shorter than they can answer in.",
                new { count = expired.Length, recent = expired.Take(5).Select(offer => new { offer.ActivityId, offer.ReservationId, offer.RingSeconds, offer.Reason, occurredUtc = Format(offer.OccurredUtc) }) }));
        }

        var declined = facts.Offers.Count(offer => offer.EventType == ContactCenterConstants.Events.OfferDeclined);

        if (declined > 0)
        {
            findings.Add(new ToolFinding(
                "OffersDeclined",
                Likelihood.Informational,
                $"{name} declined {declined} offer(s) in the last {Hours(facts.Lookback)}. A declined record is not offered to the same agent again.",
                new { count = declined }));
        }

        var accepted = facts.Offers.Count(offer => offer.EventType == ContactCenterConstants.Events.OfferAccepted);

        if (accepted > 0 && expired.Length == 0)
        {
            findings.Add(new ToolFinding(
                "OffersAccepted",
                Likelihood.Informational,
                $"{name} accepted {accepted} offer(s) in the last {Hours(facts.Lookback)}, so work did reach them recently.",
                new { count = accepted }));
        }
    }

    private static void AddDecisionFindings(List<ToolFinding> findings, AgentCallFacts facts)
    {
        foreach (var group in facts.Decisions
            .GroupBy(decision => (decision.Kind, decision.Reason))
            .Select(group => (group.Key.Kind, group.Key.Reason, Count: group.Sum(decision => Math.Max(1, decision.Count)), Last: group.Max(decision => decision.LastSeenUtc)))
            .Where(group => !string.Equals(group.Reason, "Selected", StringComparison.Ordinal))
            .OrderByDescending(group => group.Count)
            .Take(5))
        {
            var meaning = DecisionPatterns.Explain(group.Reason);

            findings.Add(new ToolFinding(
                $"Decision.{group.Kind}.{group.Reason}",
                group.Kind == "Suppression" && DecisionPatterns.IsTerminal(group.Reason) ? Likelihood.Likely : Likelihood.Possible,
                $"{group.Count} {group.Kind.ToLowerInvariant()} decision(s) for this agent's work with reason {group.Reason} in the last {Hours(facts.Lookback)}, the last at {Format(group.Last)}." +
                    (meaning is null ? string.Empty : $" {meaning}"),
                new { group.Kind, group.Reason, group.Count, lastSeenUtc = Format(group.Last) }));
        }
    }

    private static string BuildVerdict(string name, IReadOnlyList<AgentQueueEligibility> queues, IReadOnlyList<ToolFinding> findings)
    {
        var certain = findings.Where(finding => finding.Likelihood == Likelihood.Certain).ToArray();

        if (certain.Length > 0)
        {
            return $"{name} is not getting calls. The reason{(certain.Length > 1 ? "s" : string.Empty)}: " + string.Join(" ", certain.Take(3).Select(finding => finding.Summary));
        }

        var likely = findings.Where(finding => finding.Likelihood == Likelihood.Likely).ToArray();

        if (likely.Length > 0)
        {
            return $"Nothing stops {name} from being offered work, so the most likely reason they are not getting calls: " + string.Join(" ", likely.Take(2).Select(finding => finding.Summary));
        }

        return queues.Any(queue => queue.Eligible)
            ? $"{name} is eligible for work and nothing blocks them. If they are not getting calls, the cause is outside the agent: check that work is waiting for them with the queue inventory tool."
            : $"No blocking reason was found for {name}, but they are not eligible for any queue or campaign in the question. Check their queues and campaigns.";
    }

    private static bool Matches(AgentQueueEligibility queue, string filter)
        => string.Equals(queue.QueueId, filter, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(ContactCenterConstants.CampaignQueue.GetCampaignId(queue.QueueId), filter, StringComparison.OrdinalIgnoreCase) ||
            (queue.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

    private static string Describe(AgentAvailabilityDiagnosis diagnosis)
    {
        var parts = diagnosis.SignedInQueues.Concat(diagnosis.SignedInCampaigns.Select(campaign => $"campaign {campaign}")).ToArray();

        return parts.Length == 0 ? "nothing" : string.Join(", ", parts);
    }

    private static string Words(AgentAvailabilityFlagKind kind)
        => kind switch
        {
            AgentAvailabilityFlagKind.UnresolvedSoftPhoneCall => "a soft-phone call record is still open although the provider no longer has the call",
            AgentAvailabilityFlagKind.OpenInteractionAfterCallEnded => "an interaction is still open although the provider says the call ended",
            AgentAvailabilityFlagKind.BusyWithoutLiveInteraction => "the agent is Busy with no live interaction behind it",
            AgentAvailabilityFlagKind.ReservedWithoutLiveInteraction => "the agent is Reserved with no live offer behind it",
            AgentAvailabilityFlagKind.StaleReservation => "a reservation is held that no live offer or call backs",
            AgentAvailabilityFlagKind.StaleAssignedWork => "work is assigned to the agent with no offer or call behind it",
            _ => kind.ToString(),
        };

    private static string Hours(TimeSpan lookback)
        => lookback.TotalHours >= 1 ? $"{Math.Round(lookback.TotalHours)} hour(s)" : $"{Math.Round(lookback.TotalMinutes)} minute(s)";

    private static string Format(DateTime? value)
        => ContactCenterAIToolBase.Utc(value) ?? "never";
}
