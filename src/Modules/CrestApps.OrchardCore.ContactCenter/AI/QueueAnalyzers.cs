using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Words the reasons a record is held back, for a model to read.
/// </summary>
internal static class BlockerWords
{
    /// <summary>
    /// Describes a reason a record or a queue is not being dispatched.
    /// </summary>
    /// <param name="blocker">The reason.</param>
    /// <returns>The words for it.</returns>
    public static string Describe(DispatchBlocker blocker)
        => blocker switch
        {
            DispatchBlocker.DialableNow => "dialable now",
            DispatchBlocker.QueueDisabled => "the queue is turned off",
            DispatchBlocker.BusinessHoursClosed => "the queue's business hours are closed",
            DispatchBlocker.NotQueued => "not in the queue, so routing never sees it",
            DispatchBlocker.ProfileDisabled => "the dialer profile is turned off",
            DispatchBlocker.ScheduledForLater => "scheduled for later",
            DispatchBlocker.RetryCoolDown => "waiting out the retry delay",
            DispatchBlocker.AttemptsExhausted => "out of attempts",
            DispatchBlocker.NoDestination => "no number to dial",
            DispatchBlocker.DoNotCall => "do not call",
            DispatchBlocker.NationalDoNotCallRegistry => "on the national do-not-call registry",
            DispatchBlocker.OutsideCallingWindow => "outside the calling window",
            DispatchBlocker.NumberNotInService => "number not in service",
            DispatchBlocker.ComplianceScreeningUnavailable => "compliance check unavailable",
            DispatchBlocker.AbandonmentRateExceeded => "abandonment rate cap reached",
            DispatchBlocker.ProfileMissing => "the dialer profile it is stamped with no longer exists",
            DispatchBlocker.AssignedToUser => "assigned to a user for manual work",
            _ => blocker.ToString(),
        };
}

/// <summary>
/// What was read about one campaign for the inventory.
/// </summary>
/// <param name="CampaignId">The campaign's identifier.</param>
/// <param name="Name">The campaign's name.</param>
/// <param name="Summary">Its pending records split by what the dialer can do with them.</param>
/// <param name="Rows">Its queue rows: the stamped dialer profiles with what waits, is reserved and is assigned.</param>
/// <param name="SignedInAgents">How many agents are signed in to it.</param>
/// <param name="AvailableAgents">How many of them are Available.</param>
internal sealed record CampaignInventoryFacts(
    string CampaignId,
    string Name,
    DialerCampaignDispatchSummary Summary,
    IReadOnlyList<DialerQueueInventoryRow> Rows,
    int SignedInAgents,
    int AvailableAgents);

/// <summary>
/// What the analyzer concluded about a campaign.
/// </summary>
/// <param name="Verdict">The answer.</param>
/// <param name="Findings">The findings, most likely first.</param>
internal sealed record InventoryAnalysis(string Verdict, IReadOnlyList<ToolFinding> Findings);

/// <summary>
/// Explains what a campaign holds: how many records wait, are held back and why, are not in any queue and are assigned to
/// a user for manual work.
/// </summary>
internal static class CampaignInventoryAnalyzer
{
    /// <summary>
    /// Analyzes one campaign.
    /// </summary>
    /// <param name="facts">What was read about it.</param>
    /// <returns>The verdict and findings.</returns>
    public static InventoryAnalysis Analyze(CampaignInventoryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var summary = facts.Summary;
        var queued = facts.Rows.Where(row => row.QueueId is not null).ToArray();
        var inQueue = queued.Sum(row => row.WaitingCount + row.ReservedCount + row.AssignedCount);
        var findings = new List<ToolFinding>();

        if (summary.PendingCount > 0 && inQueue == 0)
        {
            findings.Add(new ToolFinding(
                "NoQueue",
                Likelihood.Certain,
                $"Campaign '{facts.Name}' has {summary.PendingCount} pending record(s) but no queue holds any of them: nothing is waiting, reserved or assigned in its queue, so no agent can be offered work from it. " +
                    $"{summary.AssignedToUserCount} are assigned to a user for manual work from their own list, {summary.NotInAnyQueueCount} are in no queue at all.",
                new { summary.PendingCount, summary.NotInAnyQueueCount, summary.AssignedToUserCount }));
        }
        else if (summary.NotInAnyQueueCount > 0)
        {
            findings.Add(new ToolFinding(
                "NotInAnyQueue",
                Likelihood.Certain,
                $"{summary.NotInAnyQueueCount} pending record(s) of campaign '{facts.Name}' are in no queue, so routing never sees them. {inQueue} other record(s) are in its queue.",
                new { summary.NotInAnyQueueCount, inQueue }));
        }

        if (summary.AssignedToUserCount > 0 && inQueue > 0)
        {
            findings.Add(new ToolFinding(
                "AssignedToUsers",
                Likelihood.Informational,
                $"{summary.AssignedToUserCount} pending record(s) are assigned to a user for manual work from their own list, so the dialer never offers them to anyone else.",
                new { summary.AssignedToUserCount }));
        }

        if (summary.PendingCount > 0 && summary.DialableNowCount == 0 && summary.NotInAnyQueueCount == 0 && summary.AssignedToUserCount == 0)
        {
            var reasons = string.Join(", ", summary.Buckets
                .Where(bucket => bucket.Blocker != DispatchBlocker.DialableNow)
                .Take(5)
                .Select(bucket => $"{bucket.Count} {BlockerWords.Describe(bucket.Blocker)}"));

            findings.Add(new ToolFinding(
                "NothingDialable",
                Likelihood.Certain,
                $"None of the {summary.PendingCount} pending record(s) of campaign '{facts.Name}' can be dialed right now: {reasons}.",
                new { buckets = summary.Buckets.Select(bucket => new { reason = bucket.Blocker.ToString(), bucket.Count, nextDueUtc = ContactCenterAIToolBase.Utc(bucket.NextDueUtc) }) }));
        }

        if (summary.ScheduledForLaterCount > 0)
        {
            findings.Add(new ToolFinding(
                "ScheduledForLater",
                Likelihood.Informational,
                $"{summary.ScheduledForLaterCount} record(s) wait for a time set on them" +
                    (summary.NextDueUtc.HasValue ? $"; the first is due at {ContactCenterAIToolBase.Utc(summary.NextDueUtc)}." : "."),
                new { count = summary.ScheduledForLaterCount, nextDueUtc = ContactCenterAIToolBase.Utc(summary.NextDueUtc) }));
        }

        foreach (var bucket in summary.OtherHeldBackReasons.Take(5))
        {
            findings.Add(new ToolFinding(
                $"HeldBack.{bucket.Blocker}",
                bucket.Blocker is DispatchBlocker.NoDestination or DispatchBlocker.ProfileDisabled or DispatchBlocker.ProfileMissing or DispatchBlocker.QueueDisabled
                    ? Likelihood.Certain
                    : Likelihood.Likely,
                $"{bucket.Count} record(s) are held back because {BlockerWords.Describe(bucket.Blocker)}" +
                    (bucket.NextDueUtc.HasValue ? $"; the first becomes due at {ContactCenterAIToolBase.Utc(bucket.NextDueUtc)}." : "."),
                new { reason = bucket.Blocker.ToString(), bucket.Count, nextDueUtc = ContactCenterAIToolBase.Utc(bucket.NextDueUtc) }));
        }

        if (summary.DialableNowCount > 0 && facts.SignedInAgents == 0)
        {
            findings.Add(new ToolFinding(
                "NobodySignedIn",
                Likelihood.Certain,
                $"{summary.DialableNowCount} record(s) of campaign '{facts.Name}' can be dialed now but no agent is signed in to it.",
                new { summary.DialableNowCount }));
        }
        else if (summary.DialableNowCount > 0 && facts.AvailableAgents == 0)
        {
            findings.Add(new ToolFinding(
                "NoAvailableAgent",
                Likelihood.Likely,
                $"{summary.DialableNowCount} record(s) of campaign '{facts.Name}' can be dialed now; {facts.SignedInAgents} agent(s) are signed in but none is Available.",
                new { summary.DialableNowCount, facts.SignedInAgents }));
        }
        else if (summary.DialableNowCount > 0)
        {
            findings.Add(new ToolFinding(
                "Dialable",
                Likelihood.Informational,
                $"{summary.DialableNowCount} record(s) can be dialed now and {facts.AvailableAgents} of {facts.SignedInAgents} signed-in agent(s) are Available.",
                new { summary.DialableNowCount, facts.AvailableAgents, facts.SignedInAgents }));
        }

        if (summary.NotEvaluatedCount > 0)
        {
            findings.Add(new ToolFinding(
                "NotEvaluated",
                Likelihood.Informational,
                $"{summary.NotEvaluatedCount} record(s) were counted but not explained, because one pass reads a limited number of records.",
                new { summary.NotEvaluatedCount }));
        }

        var ordered = ToolFinding.Order(findings);
        var first = ordered.FirstOrDefault(finding => finding.Likelihood is Likelihood.Certain or Likelihood.Likely);

        return new InventoryAnalysis(
            first is null
                ? $"Campaign '{facts.Name}': {summary.PendingCount} pending, {summary.DialableNowCount} dialable now, {summary.HeldBackCount} held back."
                : $"Campaign '{facts.Name}': {summary.PendingCount} pending, {summary.DialableNowCount} dialable now, {summary.HeldBackCount} held back. {first.Summary}",
            ordered);
    }
}

/// <summary>
/// What was read about one queue or campaign for the dispatch diagnostics.
/// </summary>
/// <param name="Diagnosis">What the dispatch diagnostics service found.</param>
/// <param name="Name">The queue's or campaign's name.</param>
/// <param name="LastRoutingDecisionUtc">When routing last decided for the queue, or <see langword="null"/> when it never has.</param>
/// <param name="SignedInAgents">How many agents are signed in to it.</param>
/// <param name="AvailableAgents">How many of them are Available.</param>
/// <param name="PacingState">What the paced dialer is doing for it, when it is paced; otherwise <see langword="null"/>.</param>
/// <param name="PacingStalledMinutes">How long no pacing cycle completed, when the dialer stalled.</param>
/// <param name="PacingLockNotAcquiredSinceUtc">Since when every pacing cycle skipped itself for want of the lock.</param>
/// <param name="NowUtc">The time it was read.</param>
internal sealed record QueueDispatchFacts(
    QueueDispatchDiagnostics Diagnosis,
    string Name,
    DateTime? LastRoutingDecisionUtc,
    int SignedInAgents,
    int AvailableAgents,
    string PacingState,
    int? PacingStalledMinutes,
    DateTime? PacingLockNotAcquiredSinceUtc,
    DateTime NowUtc);

/// <summary>
/// Says whether routing and the dialer are doing anything for a queue that has work, and if not why.
/// </summary>
internal static class DispatchAnalyzer
{
    /// <summary>
    /// How long a queue with work due may go without a routing decision before it counts as quiet.
    /// </summary>
    public static readonly TimeSpan QuietAfter = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Analyzes one queue.
    /// </summary>
    /// <param name="facts">What was read about it.</param>
    /// <returns>The verdict and findings.</returns>
    public static InventoryAnalysis Analyze(QueueDispatchFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        var diagnosis = facts.Diagnosis;
        var label = diagnosis.CampaignId is null ? $"Queue '{facts.Name}'" : $"Campaign '{facts.Name}'";
        var findings = new List<ToolFinding>();
        var workDue = diagnosis.CampaignId is null
            ? diagnosis.WaitingItemCount > 0 || diagnosis.PendingCount > 0
            : diagnosis.DialableNowCount > 0 || diagnosis.WaitingItemCount > 0;

        if (diagnosis.QueueBlocker is { } blocker)
        {
            findings.Add(new ToolFinding(
                $"Queue.{blocker}",
                Likelihood.Certain,
                $"{label} hands nobody work because {BlockerWords.Describe(blocker)}.",
                new { blocker = blocker.ToString() }));
        }

        if (facts.PacingLockNotAcquiredSinceUtc.HasValue)
        {
            findings.Add(new ToolFinding(
                "Pacing.LockNotAcquired",
                Likelihood.Certain,
                $"{label}: the paced dialer cannot take the queue's pacing lock, so every cycle skips itself. This has lasted since {ContactCenterAIToolBase.Utc(facts.PacingLockNotAcquiredSinceUtc)}. Calls are not placed until the lock holder is cleared.",
                new { sinceUtc = ContactCenterAIToolBase.Utc(facts.PacingLockNotAcquiredSinceUtc) }));
        }
        else if (facts.PacingStalledMinutes is { } stalled)
        {
            findings.Add(new ToolFinding(
                "Pacing.Stalled",
                Likelihood.Certain,
                $"{label}: work is waiting but no pacing cycle completed for {stalled} minute(s). The dialer for this campaign has stopped.",
                new { stalledMinutes = stalled }));
        }

        if (workDue && facts.SignedInAgents == 0)
        {
            findings.Add(new ToolFinding(
                "NobodySignedIn",
                Likelihood.Certain,
                $"{label} has work due but no agent is signed in to it.",
                new { diagnosis.WaitingItemCount, diagnosis.DialableNowCount }));
        }
        else if (workDue && facts.AvailableAgents == 0)
        {
            findings.Add(new ToolFinding(
                "NoAvailableAgent",
                Likelihood.Likely,
                $"{label} has work due; {facts.SignedInAgents} agent(s) are signed in but none is Available.",
                new { facts.SignedInAgents }));
        }

        if (diagnosis.RoutingFinding is { } routing)
        {
            var meaning = DecisionPatterns.Explain(routing.Reason);

            findings.Add(new ToolFinding(
                "Routing.LastDecision",
                Likelihood.Likely,
                $"Routing's latest decision for {label} was '{routing.Reason}' at {ContactCenterAIToolBase.Utc(routing.OccurredUtc)} with {routing.CandidateCount} candidate agent(s)." +
                    (meaning is null ? string.Empty : $" {meaning}") +
                    (routing.CandidateReasons.Count == 0 ? string.Empty : $" Why candidates were passed over: {string.Join(", ", routing.CandidateReasons.Select(reason => $"{reason.AgentCount} x {reason.Reason}"))}."),
                routing));
        }

        if (workDue && diagnosis.QueueBlocker is null && facts.SignedInAgents > 0)
        {
            var quiet = facts.LastRoutingDecisionUtc is null || facts.NowUtc - facts.LastRoutingDecisionUtc.Value > QuietAfter;

            if (quiet)
            {
                findings.Add(new ToolFinding(
                    "Routing.Quiet",
                    Likelihood.Likely,
                    facts.LastRoutingDecisionUtc is null
                        ? $"{label} has work due and agents signed in, but routing has never recorded a decision for it. Routing may not be running for it."
                        : $"{label} has work due and agents signed in, but routing last decided for it at {ContactCenterAIToolBase.Utc(facts.LastRoutingDecisionUtc)}, more than {QuietAfter.TotalMinutes:0} minutes ago. Routing may not be running for it.",
                    new { lastRoutingDecisionUtc = ContactCenterAIToolBase.Utc(facts.LastRoutingDecisionUtc) }));
            }
        }

        if (diagnosis.CampaignId is not null && diagnosis.PendingCount > 0 && diagnosis.DialableNowCount == 0)
        {
            var reasons = string.Join(", ", diagnosis.Buckets
                .Where(bucket => bucket.Blocker != DispatchBlocker.DialableNow)
                .Take(5)
                .Select(bucket => $"{bucket.Count} {BlockerWords.Describe(bucket.Blocker)}"));

            findings.Add(new ToolFinding(
                "NothingDialable",
                Likelihood.Certain,
                $"None of {label}'s {diagnosis.PendingCount} pending record(s) can be dialed now: {(reasons.Length == 0 ? "none were explained" : reasons)}.",
                new { buckets = diagnosis.Buckets.Select(bucket => new { reason = bucket.Blocker.ToString(), bucket.Count }) }));
        }

        var ordered = ToolFinding.Order(findings);
        var problem = ordered.FirstOrDefault(finding => finding.Likelihood is Likelihood.Certain or Likelihood.Likely);

        return new InventoryAnalysis(
            problem is null
                ? $"{label} is working: {(workDue ? "work is due and nothing blocks it" : "no work is due")}."
                : $"{label} has a problem. {problem.Summary}",
            ordered);
    }

    /// <summary>
    /// Says whether a queue is quiet: it has work due, nobody is handling it and no blocker or routing problem explains it.
    /// </summary>
    /// <param name="analysis">The queue's analysis.</param>
    /// <returns><see langword="true"/> when the queue has a finding that says work is due and nothing is happening.</returns>
    public static bool IsQuiet(InventoryAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        return analysis.Findings.Any(finding => finding.Code is "Routing.Quiet" or "Pacing.Stalled" or "Pacing.LockNotAcquired" or "NobodySignedIn" or "NoAvailableAgent");
    }
}
