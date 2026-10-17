using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides whether a failed routing decision repeats the last one recorded for its queue, and reads how many decisions
/// a routing event stands for and when the last of them was made.
/// </summary>
/// <remarks>
/// While a queue has due work and nobody can take it, routing makes the same failed decision on every pass, about once
/// a minute. Recording each one wrote an event and an outbox message per queue per minute that said nothing new.
/// Folding a repeat into the event that first recorded it keeps the decision, how often it was made and when it was
/// last made, at the cost of one row. A decision that differs in any way - another record, another reason, another
/// candidate or another candidate reason, or any success - starts a new event, so nothing a reader could tell apart is
/// merged. A run is also cut after <see cref="MaximumGap"/> without a repeat, and after <see cref="MaximumSpan"/> in
/// all, so an hourly count stays close and retention, which removes events by their first time, never removes a run
/// that is still being repeated.
/// </remarks>
internal static class RoutingDecisionCoalescing
{
    /// <summary>
    /// The longest pause between two decisions that are still folded into one event.
    /// </summary>
    public static readonly TimeSpan MaximumGap = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The longest time one event may cover, from its first decision to the decision being folded into it.
    /// </summary>
    public static readonly TimeSpan MaximumSpan = TimeSpan.FromHours(1);

    /// <summary>
    /// Decides whether a decision repeats the latest recorded decision of its queue.
    /// </summary>
    /// <param name="previousEvent">The latest routing decision event recorded for the queue.</param>
    /// <param name="previous">The payload of <paramref name="previousEvent"/>.</param>
    /// <param name="next">The decision routing has just made.</param>
    /// <param name="decidedUtc">The UTC time <paramref name="next"/> was made.</param>
    /// <returns><see langword="true"/> when <paramref name="next"/> may be folded into <paramref name="previousEvent"/>.</returns>
    public static bool IsRepeat(
        InteractionEvent previousEvent,
        ActivityRoutingDecisionEventData previous,
        ActivityRoutingDecisionEventData next,
        DateTime decidedUtc)
    {
        if (previousEvent is null || previous is null || next is null)
        {
            return false;
        }

        if (previous.Succeeded || next.Succeeded || string.IsNullOrEmpty(next.QueueId))
        {
            return false;
        }

        if (!string.Equals(previous.QueueId, next.QueueId, StringComparison.Ordinal) ||
            !string.Equals(previous.QueueItemId, next.QueueItemId, StringComparison.Ordinal) ||
            !string.Equals(previous.ActivityItemId, next.ActivityItemId, StringComparison.Ordinal) ||
            !string.Equals(previous.SelectedAgentId, next.SelectedAgentId, StringComparison.Ordinal) ||
            !string.Equals(previous.Reason, next.Reason, StringComparison.Ordinal))
        {
            return false;
        }

        if (decidedUtc - GetLastSeenUtc(previousEvent, previous) > MaximumGap)
        {
            return false;
        }

        if (decidedUtc - previousEvent.OccurredUtc >= MaximumSpan)
        {
            return false;
        }

        return Fingerprint(previous.Candidates).SequenceEqual(Fingerprint(next.Candidates), StringComparer.Ordinal);
    }

    /// <summary>
    /// Reads how many decisions a routing event stands for. An event written before decisions were coalesced stands for
    /// one.
    /// </summary>
    /// <param name="data">The payload of the routing decision event.</param>
    /// <returns>The number of decisions, at least one.</returns>
    public static int GetOccurrenceCount(ActivityRoutingDecisionEventData data)
        => data is not null && data.OccurrenceCount > 0
            ? data.OccurrenceCount
            : 1;

    /// <summary>
    /// Reads when the last decision a routing event stands for was made. An event that stands for one decision was last
    /// made when it occurred.
    /// </summary>
    /// <param name="interactionEvent">The routing decision event.</param>
    /// <param name="data">The payload of <paramref name="interactionEvent"/>.</param>
    /// <returns>The UTC time of the last decision.</returns>
    public static DateTime GetLastSeenUtc(InteractionEvent interactionEvent, ActivityRoutingDecisionEventData data)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        return data?.LastSeenUtc is { } lastSeenUtc && lastSeenUtc > interactionEvent.OccurredUtc
            ? lastSeenUtc
            : interactionEvent.OccurredUtc;
    }

    // Who was weighed, whether each could take the work and why: the part of a decision a reader compares. The score is
    // left out, since an agent's idle time moves it on every pass without changing anything the decision says.
    private static List<string> Fingerprint(IEnumerable<ActivityRoutingCandidateDecisionData> candidates)
    {
        var fingerprint = new List<string>();

        if (candidates is null)
        {
            return fingerprint;
        }

        foreach (var candidate in candidates)
        {
            if (candidate is null)
            {
                continue;
            }

            var reasons = candidate.Reasons is null
                ? string.Empty
                : string.Join("\u001F", candidate.Reasons);

            fingerprint.Add($"{candidate.AgentId}\u001E{(candidate.IsEligible ? "1" : "0")}\u001E{reasons}");
        }

        fingerprint.Sort(StringComparer.Ordinal);

        return fingerprint;
    }
}
