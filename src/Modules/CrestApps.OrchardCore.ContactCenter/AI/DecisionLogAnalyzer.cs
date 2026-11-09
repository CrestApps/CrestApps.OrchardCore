using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// One reason in the decision log with how often it was decided.
/// </summary>
/// <param name="Kind">Routing or Suppression.</param>
/// <param name="Reason">The reason's name.</param>
/// <param name="Meaning">What the reason means in plain words, when known.</param>
/// <param name="Decisions">How many times it was decided, counting repeats.</param>
/// <param name="Events">How many log entries recorded it.</param>
/// <param name="LastSeenUtc">When it was last decided.</param>
internal sealed record DecisionReasonRow(string Kind, string Reason, string Meaning, int Decisions, int Events, DateTime LastSeenUtc);

/// <summary>
/// What the decision log analyzer concluded.
/// </summary>
/// <param name="Verdict">The answer.</param>
/// <param name="Findings">The findings, most likely first.</param>
/// <param name="Reasons">The decisions summed up by reason, most frequent first.</param>
/// <param name="Drains">The stretches in which many records were suppressed in seconds.</param>
internal sealed record DecisionLogAnalysis(string Verdict, IReadOnlyList<ToolFinding> Findings, IReadOnlyList<DecisionReasonRow> Reasons, IReadOnlyList<DecisionDrain> Drains);

/// <summary>
/// Sums up dialer decisions by reason and calls out the pattern of a queue drained in seconds by one suppression reason.
/// </summary>
internal static class DecisionLogAnalyzer
{
    /// <summary>
    /// Analyzes a set of decision log entries.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="names">The names of the campaigns and queues by queue identifier, for the words of a finding.</param>
    /// <returns>The analysis.</returns>
    public static DecisionLogAnalysis Analyze(IReadOnlyCollection<DialerDecisionIndex> entries, IReadOnlyDictionary<string, string> names)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(names);

        var reasons = DialerDecisionReasonSummary.Summarize(entries)
            .Select(row => new DecisionReasonRow(
                Kind(row.EventType),
                row.Reason,
                DecisionPatterns.Explain(row.Reason),
                row.Decisions,
                row.Events,
                row.LastSeenUtc))
            .ToArray();
        var drains = DecisionPatterns.FindDrains(entries);
        var findings = new List<ToolFinding>();

        foreach (var drain in drains)
        {
            var name = NameOf(names, drain.Scope);
            var terminal = DecisionPatterns.IsTerminal(drain.Reason);

            findings.Add(new ToolFinding(
                $"Drain.{drain.Reason}",
                Likelihood.Certain,
                $"{drain.Activities} record(s) of {name} were suppressed for {drain.Reason} within {Math.Round(drain.Seconds)} second(s), between {ContactCenterAIToolBase.Utc(drain.FirstUtc)} and {ContactCenterAIToolBase.Utc(drain.LastUtc)}. " +
                    (terminal
                        ? "This is a drain: the dialer fails a record with no number for good, so the whole batch left the queue at once and agents ran out of work. Fix the records' channel or number, or the contacts' numbers, and reload them."
                        : "A burst like this means one cause hit many records at once, not many separate problems.") +
                    (DecisionPatterns.Explain(drain.Reason) is { } meaning ? $" {meaning}" : string.Empty),
                new { scope = drain.Scope, name, drain.Reason, activities = drain.Activities, seconds = Math.Round(drain.Seconds), firstUtc = ContactCenterAIToolBase.Utc(drain.FirstUtc), lastUtc = ContactCenterAIToolBase.Utc(drain.LastUtc) }));
        }

        foreach (var row in reasons.Where(row => row.Kind == "Suppression").Take(3))
        {
            if (drains.Any(drain => drain.Reason == row.Reason))
            {
                continue;
            }

            findings.Add(new ToolFinding(
                $"Suppressed.{row.Reason}",
                Likelihood.Likely,
                $"{row.Decisions} dial(s) were suppressed for {row.Reason}, the last at {ContactCenterAIToolBase.Utc(row.LastSeenUtc)}." + (row.Meaning is null ? string.Empty : $" {row.Meaning}"),
                row));
        }

        foreach (var row in reasons.Where(row => row.Kind == "Routing" && row.Reason is not "Selected").Take(3))
        {
            findings.Add(new ToolFinding(
                $"Routing.{row.Reason}",
                Likelihood.Likely,
                $"Routing found nothing to assign {row.Decisions} time(s) with reason {row.Reason}, the last at {ContactCenterAIToolBase.Utc(row.LastSeenUtc)}." + (row.Meaning is null ? string.Empty : $" {row.Meaning}"),
                row));
        }

        var ordered = ToolFinding.Order(findings);
        var total = reasons.Sum(row => row.Decisions);

        var verdict = entries.Count == 0
            ? "No dialer decisions match, so routing and the compliance gate recorded nothing for this filter in the period. Either nothing was due or the filter is too narrow."
            : drains.Count > 0
                ? $"Out of {total} decision(s), {ordered[0].Summary}"
                : $"{total} decision(s): " + string.Join(", ", reasons.Take(4).Select(row => $"{row.Decisions} x {row.Reason}")) + ".";

        return new DecisionLogAnalysis(verdict, ordered, reasons, drains);
    }

    private static string Kind(string eventType)
        => eventType == ContactCenterConstants.Events.DialSuppressed ? "Suppression" : "Routing";

    private static string NameOf(IReadOnlyDictionary<string, string> names, string scope)
    {
        if (string.IsNullOrEmpty(scope))
        {
            return "the queue";
        }

        var queueId = ContactCenterConstants.IsCampaignQueue(scope) ? scope : ContactCenterConstants.CampaignQueue.CreateId(scope);

        if (names.TryGetValue(queueId, out var campaignName))
        {
            return $"campaign '{campaignName}'";
        }

        return names.TryGetValue(scope, out var queueName) ? $"queue '{queueName}'" : $"'{scope}'";
    }
}
