using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// The compliance gate's verdict on a record, as the dialer would give it now.
/// </summary>
/// <param name="Eligible">Whether the dialer may dial the record.</param>
/// <param name="Reason">The suppression reason's name when it may not.</param>
/// <param name="Description">The gate's own words.</param>
/// <param name="DestinationResolvedFromContact">Whether the record had no number of its own and the gate took the contact's current number.</param>
/// <param name="DestinationLast4">The last four digits of the number the gate resolved, or <see langword="null"/>.</param>
internal sealed record ActivityEligibility(bool Eligible, string Reason, string Description, bool DestinationResolvedFromContact, string DestinationLast4);

/// <summary>
/// The queue item that holds an activity in a queue.
/// </summary>
/// <param name="ItemId">The queue item's identifier.</param>
/// <param name="QueueId">The queue.</param>
/// <param name="Status">Waiting, reserved or assigned.</param>
/// <param name="DialerProfileId">The dialer profile stamped on it.</param>
/// <param name="AgentId">The agent it is reserved for or assigned to.</param>
/// <param name="EnqueuedUtc">When it entered the queue.</param>
internal sealed record ActivityQueueItem(string ItemId, string QueueId, string Status, string DialerProfileId, string AgentId, DateTime EnqueuedUtc);

/// <summary>
/// The latest call of an activity.
/// </summary>
/// <param name="InteractionId">The interaction's identifier.</param>
/// <param name="Status">Its status.</param>
/// <param name="AgentId">The agent who handled it.</param>
/// <param name="CreatedUtc">When it was created.</param>
/// <param name="EndedUtc">When it ended.</param>
/// <param name="Settled">Whether it is finished.</param>
/// <param name="AwaitingWrapUp">Whether the call is over and the after-call work is not finished.</param>
internal sealed record ActivityInteraction(string InteractionId, string Status, string AgentId, DateTime CreatedUtc, DateTime? EndedUtc, bool Settled, bool AwaitingWrapUp);

/// <summary>
/// One decision the dialer made about an activity.
/// </summary>
/// <param name="Kind">Routing or Suppression.</param>
/// <param name="Reason">The reason.</param>
/// <param name="Count">How many times it was made.</param>
/// <param name="LastSeenUtc">When it was last made.</param>
internal sealed record ActivityDecision(string Kind, string Reason, int Count, DateTime LastSeenUtc);

/// <summary>
/// Everything read about one activity, which <see cref="ActivityAnalyzer"/> turns into findings.
/// </summary>
internal sealed record ActivityFacts
{
    public string ActivityId { get; init; }

    public string Status { get; init; }

    public bool IsPending { get; init; }

    public bool IsAutomated { get; init; }

    public string TerminalReasonCode { get; init; }

    public string Channel { get; init; }

    public string StoredDestinationLast4 { get; init; }

    public bool HasStoredDestination { get; init; }

    public string CampaignId { get; init; }

    public string CampaignName { get; init; }

    public string DialerProfileId { get; init; }

    public string DialerProfileName { get; init; }

    public string DialerMode { get; init; }

    public bool? DialerProfileEnabled { get; init; }

    public string AssignedToUserName { get; init; }

    public string ReservedByUserName { get; init; }

    public DateTime? ReservationExpiresUtc { get; init; }

    public int Attempts { get; init; }

    public int? MaxAttempts { get; init; }

    public DateTime ScheduledUtc { get; init; }

    public ActivityQueueItem QueueItem { get; init; }

    public IReadOnlyList<AgentReservation> Reservations { get; init; } = [];

    public ActivityInteraction LastInteraction { get; init; }

    public DispatchBlocker? Blocker { get; init; }

    public DateTime? BlockerDueUtc { get; init; }

    public ActivityEligibility Eligibility { get; init; }

    public IReadOnlyList<ActivityDecision> Decisions { get; init; } = [];

    public DateTime NowUtc { get; init; }
}

/// <summary>
/// Says why one activity is, or is not, being dialed or offered. Each finding rests on a record the dialer itself reads.
/// </summary>
internal static class ActivityAnalyzer
{
    /// <summary>
    /// Analyzes one activity.
    /// </summary>
    /// <param name="facts">What was read about it.</param>
    /// <returns>The verdict and findings.</returns>
    public static InventoryAnalysis Analyze(ActivityFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var findings = new List<ToolFinding>();
        var dialable = true;

        if (!facts.IsPending)
        {
            dialable = false;
            findings.Add(new ToolFinding(
                "NotPending",
                Likelihood.Certain,
                $"The activity's status is {facts.Status}" +
                    (string.IsNullOrEmpty(facts.TerminalReasonCode) ? string.Empty : $" (reason '{facts.TerminalReasonCode}')") +
                    ", so it is not waiting to be dialed or offered." +
                    (facts.Status is "Failed" or "Cancelled" or "Completed" or "Purged" ? " A closed activity is never retried by the dialer; reopen or recreate it to work it again." : " It is in the middle of being worked."),
                new { facts.Status, facts.TerminalReasonCode }));
        }

        if (facts.IsAutomated)
        {
            findings.Add(new ToolFinding(
                "Automated",
                Likelihood.Informational,
                "This is an automated (AI) activity, owned by the AI voice processor rather than by agent routing, so queues and reservations do not apply to it.",
                null));
        }

        foreach (var reservation in facts.Reservations)
        {
            dialable = false;
            var expired = reservation.ExpiresUtc <= facts.NowUtc;

            findings.Add(new ToolFinding(
                expired ? "ReservationExpiredNotReleased" : "Reserved",
                Likelihood.Certain,
                expired
                    ? $"Reservation {reservation.ReservationId} (queue {reservation.QueueId}) expired at {ContactCenterAIToolBase.Utc(reservation.ExpiresUtc)} but was not released; the activity stays reserved until the expiry sweep releases it."
                    : $"The activity is reserved for an agent (reservation {reservation.ReservationId}, offered {ContactCenterAIToolBase.Utc(reservation.CreatedUtc)}, expires {ContactCenterAIToolBase.Utc(reservation.ExpiresUtc)}). Nobody else is offered it meanwhile.",
                reservation));
        }

        if (facts.LastInteraction is { Settled: false } open)
        {
            dialable = false;
            findings.Add(new ToolFinding(
                "OpenInteraction",
                Likelihood.Certain,
                open.AwaitingWrapUp
                    ? $"The last call (interaction {open.InteractionId}) ended but the agent never completed the after-call work, so the activity is still held by them."
                    : $"A call (interaction {open.InteractionId}, {open.Status}) is still open for this activity.",
                open));
        }

        if (facts.IsPending && facts.QueueItem is null)
        {
            dialable = false;
            findings.Add(facts.AssignedToUserName is not null
                ? new ToolFinding(
                    "AssignedToUser",
                    Likelihood.Certain,
                    $"The activity is assigned to {facts.AssignedToUserName} for manual work from their own list and is in no queue, so routing never offers it to anyone else.",
                    new { assignedTo = facts.AssignedToUserName })
                : new ToolFinding(
                    "NotInQueue",
                    Likelihood.Certain,
                    $"The activity has no item in {(facts.CampaignName is null ? "any queue" : $"campaign '{facts.CampaignName}''s queue")}, so routing never sees it and no agent can be offered it.",
                    null));
        }
        else if (facts.QueueItem is { } item)
        {
            findings.Add(new ToolFinding(
                "Queued",
                Likelihood.Informational,
                $"The activity is in queue {item.QueueId} as {item.Status} since {ContactCenterAIToolBase.Utc(item.EnqueuedUtc)}" +
                    (string.IsNullOrEmpty(item.AgentId) ? "." : $" for agent {item.AgentId}."),
                item));
        }

        if (facts.IsPending && facts.Blocker is { } blocker && blocker is not (DispatchBlocker.DialableNow or DispatchBlocker.NotQueued or DispatchBlocker.AssignedToUser))
        {
            dialable = false;
            findings.Add(new ToolFinding(
                $"Held.{blocker}",
                Likelihood.Certain,
                $"The dialer holds the activity back because {BlockerWords.Describe(blocker)}" +
                    (facts.BlockerDueUtc.HasValue ? $"; it becomes due at {ContactCenterAIToolBase.Utc(facts.BlockerDueUtc)}." : "."),
                new { blocker = blocker.ToString(), dueUtc = ContactCenterAIToolBase.Utc(facts.BlockerDueUtc) }));
        }

        if (facts.Eligibility is { } eligibility)
        {
            if (!eligibility.Eligible)
            {
                dialable = false;

                var channelNote = string.IsNullOrWhiteSpace(facts.Channel)
                    ? " The activity's channel is empty and its contact has no phone number to fall back on."
                    : facts.HasStoredDestination ? string.Empty : " The activity has no number of its own and its contact has none either.";

                findings.Add(new ToolFinding(
                    $"Suppressed.{eligibility.Reason}",
                    Likelihood.Certain,
                    $"If the dialer tried this activity now, the compliance gate would suppress it: {eligibility.Description}" +
                        (eligibility.Reason == "NoDestination" ? channelNote : string.Empty) +
                        (DecisionPatterns.IsTerminal(eligibility.Reason) ? " The dialer fails such a record for good." : string.Empty),
                    new { eligibility.Reason, eligibility.Description }));
            }
            else if (eligibility.DestinationResolvedFromContact)
            {
                findings.Add(new ToolFinding(
                    "DestinationFromContact",
                    Likelihood.Likely,
                    "The activity has no number of its own" +
                        (string.IsNullOrWhiteSpace(facts.Channel) ? " and its channel is empty" : string.Empty) +
                        $", but its contact has a current number (ending {eligibility.DestinationLast4}), which the dialer uses at dial time. So it is dialable; an empty channel or number on the record alone does not block it." +
                        " Fix the record's channel so reports and filters see it correctly.",
                    new { facts.Channel, destinationEndsWith = eligibility.DestinationLast4 }));
            }
        }
        else if (facts.IsPending && string.IsNullOrWhiteSpace(facts.Channel) && !facts.HasStoredDestination)
        {
            findings.Add(new ToolFinding(
                "NoDestinationOnRecord",
                Likelihood.Likely,
                "The activity has an empty channel and no number of its own. The compliance gate could not be asked because the outbound dialer is not enabled, so whether its contact has a number to fall back on is unknown.",
                null));
        }

        foreach (var group in facts.Decisions
            .GroupBy(decision => (decision.Kind, decision.Reason))
            .Select(group => (group.Key.Kind, group.Key.Reason, Count: group.Sum(decision => Math.Max(1, decision.Count)), Last: group.Max(decision => decision.LastSeenUtc)))
            .OrderByDescending(group => group.Last)
            .Take(4))
        {
            findings.Add(new ToolFinding(
                $"Decision.{group.Kind}.{group.Reason}",
                Likelihood.Informational,
                $"The dialer decided '{group.Reason}' ({group.Kind.ToLowerInvariant()}) {group.Count} time(s), last at {ContactCenterAIToolBase.Utc(group.Last)}.",
                new { group.Kind, group.Reason, group.Count, lastSeenUtc = ContactCenterAIToolBase.Utc(group.Last) }));
        }

        var ordered = ToolFinding.Order(findings);
        var blocking = ordered.Where(finding => finding.Likelihood == Likelihood.Certain).ToArray();

        var verdict = dialable && blocking.Length == 0
            ? $"Activity {facts.ActivityId} can be dialed or offered now." + (facts.QueueItem is null ? string.Empty : $" It is in queue {facts.QueueItem.QueueId}.")
            : $"Activity {facts.ActivityId} is not being dialed or offered. " + string.Join(" ", blocking.Take(3).Select(finding => finding.Summary));

        return new InventoryAnalysis(verdict, ordered);
    }
}
