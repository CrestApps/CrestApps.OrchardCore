using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Reports;

/// <summary>
/// Pure aggregation for the AI handoff / containment report: from a set of concluded automated conversations it
/// computes how many were contained by the bot versus escalated to a human, and the average time to escalate.
/// Kept free of I/O so it is fully unit-testable.
/// </summary>
public static class HandoffContainmentAggregator
{
    /// <summary>
    /// Computes the containment summary for the supplied concluded automated activities.
    /// </summary>
    /// <param name="activities">The concluded automated activities in the reporting period.</param>
    /// <returns>The summary.</returns>
    /// <summary>
    /// The reason bucket for an escalation that did not conclude as a handoff (a routed voice call escalates
    /// while it continues, so its terminal reason is the eventual call outcome rather than a handoff code).
    /// </summary>
    public const string RoutedVoiceReason = "handed_off_routed_voice";

    public static HandoffContainmentSummary Compute(IEnumerable<OmnichannelActivity> activities)
    {
        ArgumentNullException.ThrowIfNull(activities);

        var total = 0;
        var escalatedByReason = new Dictionary<string, int>(StringComparer.Ordinal);
        var handoffDurations = new List<TimeSpan>();

        foreach (var activity in activities)
        {
            total++;

            // The durable escalation flag is the source of truth — it survives a routed voice call leaving the
            // automated lane, which the terminal reason alone does not.
            if (!activity.AiEscalated)
            {
                continue;
            }

            var reasonKey = OmnichannelConstants.TerminalReasons.IsHandoff(activity.TerminalReasonCode)
                ? activity.TerminalReasonCode
                : RoutedVoiceReason;

            escalatedByReason.TryGetValue(reasonKey, out var count);
            escalatedByReason[reasonKey] = count + 1;

            if (activity.CompletedUtc is { } completed && completed >= activity.CreatedUtc)
            {
                handoffDurations.Add(completed - activity.CreatedUtc);
            }
        }

        var escalated = escalatedByReason.Values.Sum();

        return new HandoffContainmentSummary
        {
            Total = total,
            Escalated = escalated,
            EscalatedByReason = escalatedByReason,
            AverageTimeToHandoff = handoffDurations.Count > 0
                ? TimeSpan.FromTicks((long)handoffDurations.Average(duration => duration.Ticks))
                : null,
        };
    }
}

/// <summary>
/// The computed AI handoff / containment summary for a reporting period.
/// </summary>
public sealed class HandoffContainmentSummary
{
    /// <summary>
    /// Gets the total number of concluded automated conversations.
    /// </summary>
    public int Total { get; init; }

    /// <summary>
    /// Gets the number that escalated to a human.
    /// </summary>
    public int Escalated { get; init; }

    /// <summary>
    /// Gets the number contained by the bot (not escalated).
    /// </summary>
    public int Contained => Total - Escalated;

    /// <summary>
    /// Gets the containment rate (contained ÷ total), 0 when there were no conversations.
    /// </summary>
    public double ContainmentRate => Total > 0 ? (double)Contained / Total : 0;

    /// <summary>
    /// Gets the escalation rate (escalated ÷ total), 0 when there were no conversations.
    /// </summary>
    public double EscalationRate => Total > 0 ? (double)Escalated / Total : 0;

    /// <summary>
    /// Gets the escalation counts keyed by terminal reason code.
    /// </summary>
    public IReadOnlyDictionary<string, int> EscalatedByReason { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets the average time from a conversation starting to it escalating, when any escalated; otherwise null.
    /// </summary>
    public TimeSpan? AverageTimeToHandoff { get; init; }
}
