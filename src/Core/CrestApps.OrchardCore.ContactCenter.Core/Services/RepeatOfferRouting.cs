using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides who a queue item goes to when some of the agents who could take it have had it already: the agents who
/// declined it or let its offer ring out, and the agent who transferred it into the queue.
/// </summary>
/// <remarks>
/// <para>
/// An ordering, not a filter. Declining does not change how long an agent has been idle, and a direct call leaves the
/// agent who transferred it no after-call work, so without this both are still the most eligible a moment later and
/// the caller is rung straight back to them while an agent who has not had the call waits behind. So the item goes,
/// in this order, to:
/// </para>
/// <list type="number">
/// <item><description>an agent who has neither declined it nor transferred it away, chosen by the queue's strategy;</description></item>
/// <item><description>an agent who declined it before the latest decline, the earliest to decline first;</description></item>
/// <item><description>the agent who transferred it away;</description></item>
/// <item><description>the agent who declined it last, only when nobody else who can take it is available.</description></item>
/// </list>
/// <para>
/// Nobody is held back: the caller is better rung again, even by the agent who just turned them down, than left on
/// hold with an agent free. The queue's maximum wait, voicemail and overflow still move on a caller who keeps going
/// unanswered.
/// </para>
/// </remarks>
internal static class RepeatOfferRouting
{
    private const int NotHadTheCall = 0;
    private const int DeclinedEarlier = 1;
    private const int TransferredAway = 2;
    private const int DeclinedLast = 3;

    /// <summary>
    /// Leaves eligible only the candidates first in line for the item, among those every strategy found eligible.
    /// </summary>
    /// <param name="queueItem">The item being routed.</param>
    /// <param name="candidates">The candidates, after every strategy has decided who is eligible.</param>
    public static void Apply(QueueItem queueItem, IList<ActivityRoutingCandidate> candidates)
    {
        var declined = queueItem.DeclinedAgentIds ?? [];
        var transferredAway = queueItem.ExcludedAgentIds ?? [];

        if (declined.Count == 0 && transferredAway.Count == 0)
        {
            return;
        }

        var ranked = candidates
            .Where(candidate => candidate.IsEligible)
            .Select(candidate => (Candidate: candidate, Rank: RankOf(candidate.Agent.ItemId, declined, transferredAway)))
            .ToArray();

        if (ranked.Length == 0)
        {
            return;
        }

        var first = ranked.Min(entry => entry.Rank);

        foreach (var (candidate, rank) in ranked.Where(entry => entry.Rank != first))
        {
            candidate.IsEligible = false;
            candidate.AddReason(rank.Group switch
            {
                TransferredAway => "Transferred this call away, so it goes to an agent who has not had it first.",
                DeclinedLast => "Declined this call last, so it goes to anybody else who can take it first.",
                _ when first.Group == NotHadTheCall => "Declined or missed this call, so it goes to an agent who has not turned it down.",
                _ => "Declined this call more recently than the agent it is offered to next.",
            });
        }
    }

    /// <summary>
    /// Where the agent stands in line: the group they are in, then how early they declined.
    /// </summary>
    private static (int Group, int Position) RankOf(string agentId, IList<string> declined, IList<string> transferredAway)
    {
        var index = declined.IndexOf(agentId);

        // The one who has just turned the caller down is rung again only when nobody else can take them.
        if (index >= 0 && index == declined.Count - 1)
        {
            return (DeclinedLast, 0);
        }

        // Positions start at 1 so an agent who transferred the call away and never declined it comes before one who
        // did both.
        if (transferredAway.Contains(agentId, StringComparer.Ordinal))
        {
            return (TransferredAway, index + 1);
        }

        return index >= 0 ? (DeclinedEarlier, index + 1) : (NotHadTheCall, 0);
    }
}
