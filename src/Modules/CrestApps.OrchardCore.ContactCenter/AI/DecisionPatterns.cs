using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Finds patterns in the dialer decision log that one decision cannot show: many records suppressed in seconds.
/// </summary>
internal static class DecisionPatterns
{
    /// <summary>
    /// The fewest records suppressed for the same reason that count as a drain.
    /// </summary>
    public const int DrainMinimumActivities = 10;

    /// <summary>
    /// The longest stretch the records of a drain were suppressed in.
    /// </summary>
    public static readonly TimeSpan DrainWindow = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The most entries one search reads, newest first, so one question cannot read a whole tenant's log.
    /// </summary>
    public const int MaxEntries = 5_000;

    /// <summary>
    /// Finds the stretches in which many records of one campaign or queue were suppressed for the same reason in seconds.
    /// </summary>
    /// <param name="entries">The suppression entries of the decision log.</param>
    /// <returns>One drain for each campaign or queue and reason that has one, the largest first.</returns>
    public static IReadOnlyList<DecisionDrain> FindDrains(IEnumerable<DialerDecisionIndex> entries)
    {
        if (entries is null)
        {
            return [];
        }

        var drains = new List<DecisionDrain>();

        foreach (var group in entries
            .Where(entry => entry is not null &&
                string.Equals(entry.EventType, ContactCenterConstants.Events.DialSuppressed, StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(entry.ActivityItemId))
            .GroupBy(entry => (Scope: !string.IsNullOrEmpty(entry.CampaignId) ? entry.CampaignId : entry.QueueId ?? string.Empty, Reason: entry.Reason ?? string.Empty)))
        {
            var ordered = group.OrderBy(entry => entry.OccurredUtc).ToArray();

            if (ordered.Length < DrainMinimumActivities)
            {
                continue;
            }

            DecisionDrain best = null;
            var end = 0;

            for (var start = 0; start < ordered.Length; start++)
            {
                if (end < start)
                {
                    end = start;
                }

                while (end + 1 < ordered.Length && ordered[end + 1].OccurredUtc - ordered[start].OccurredUtc <= DrainWindow)
                {
                    end++;
                }

                var inWindow = ordered
                    .Skip(start)
                    .Take(end - start + 1)
                    .Select(entry => entry.ActivityItemId)
                    .Distinct(StringComparer.Ordinal)
                    .Count();

                if (inWindow >= DrainMinimumActivities && (best is null || inWindow > best.Activities))
                {
                    best = new DecisionDrain(
                        group.Key.Scope,
                        group.Key.Reason,
                        inWindow,
                        ordered[start].OccurredUtc,
                        ordered[end].OccurredUtc);
                }
            }

            if (best is not null)
            {
                drains.Add(best);
            }
        }

        return [.. drains.OrderByDescending(drain => drain.Activities).ThenBy(drain => drain.Scope, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Says whether a suppression reason ends the record: the dialer marks it failed and takes it out of the queue, so it
    /// is never retried.
    /// </summary>
    /// <param name="reason">The suppression reason's name.</param>
    /// <returns><see langword="true"/> when the record is finished for good.</returns>
    public static bool IsTerminal(string reason)
        => string.Equals(reason, nameof(DialerSuppressionReason.NoDestination), StringComparison.Ordinal);

    /// <summary>
    /// Says in plain words what a decision reason means.
    /// </summary>
    /// <param name="reason">The reason's name.</param>
    /// <returns>What the reason means.</returns>
    public static string Explain(string reason)
        => reason switch
        {
            nameof(DialerSuppressionReason.NoDestination) => "The record has no number to dial on its channel and its contact has none either. The dialer fails such a record for good, so it leaves the queue.",
            nameof(DialerSuppressionReason.MaxAttemptsReached) => "The record used all the attempts its dialer profile allows.",
            nameof(DialerSuppressionReason.RetryCoolDown) => "The record is waiting out the profile's retry delay; the dialer tries it again later.",
            nameof(DialerSuppressionReason.DoNotCall) => "The contact opted out of phone calls.",
            nameof(DialerSuppressionReason.NationalDoNotCallRegistry) => "The number is on a national do-not-call registry.",
            nameof(DialerSuppressionReason.OutsideCallingWindow) => "The number is outside the calling hours of the profile's calendar; the dialer tries it again when the window opens.",
            nameof(DialerSuppressionReason.AbandonmentRateExceeded) => "The campaign reached its abandonment rate cap, so paced dialing paused.",
            nameof(DialerSuppressionReason.ComplianceScreeningUnavailable) => "A compliance registry could not answer, so the dial was held back until it can.",
            nameof(DialerSuppressionReason.NumberNotInService) => "The number is known not to be in service.",
            "NoAgentsAvailable" => "Routing looked for an agent and none was available.",
            "NoAgentMatchedPolicy" => "Agents were available but none matched the queue's routing policy, for example a missing skill.",
            "ReservationLost" => "The agent or the record was taken by something else before the reservation was made.",
            "Selected" => "Routing picked an agent.",
            _ => null,
        };
}

/// <summary>
/// Many records of one campaign or queue suppressed for the same reason in a short time.
/// </summary>
/// <param name="Scope">The campaign or queue identifier the records belong to.</param>
/// <param name="Reason">The suppression reason's name.</param>
/// <param name="Activities">How many different records were suppressed in the stretch.</param>
/// <param name="FirstUtc">When the first of them was suppressed.</param>
/// <param name="LastUtc">When the last of them was suppressed.</param>
internal sealed record DecisionDrain(string Scope, string Reason, int Activities, DateTime FirstUtc, DateTime LastUtc)
{
    /// <summary>
    /// Gets how long the stretch lasted, in seconds.
    /// </summary>
    public double Seconds => Math.Max(0, (LastUtc - FirstUtc).TotalSeconds);
}
