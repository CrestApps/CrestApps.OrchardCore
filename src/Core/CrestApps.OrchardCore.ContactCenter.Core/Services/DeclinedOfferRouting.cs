using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Keeps a queue item away from the agents who declined it or let its offer ring out.
/// </summary>
/// <remarks>
/// Not a preference a strategy weighs. Declining does not change how long an agent has been idle, so without this the
/// agent who turned the call down is still the most eligible a moment later and is rung again, while an agent who has
/// not been offered the call waits behind them. The item goes to somebody who has not turned it down yet; only when
/// everybody who could take it has does a new round start, in the order they declined, and never straight back to the
/// agent who declined last unless nobody else can take it and <see cref="ActivityRoutingService.DeclinedOfferRetryDelay"/>
/// has passed.
/// </remarks>
internal static class DeclinedOfferRouting
{
    /// <summary>
    /// Marks the candidates the item must not be offered to now because they declined it.
    /// </summary>
    /// <param name="queueItem">The item being routed.</param>
    /// <param name="candidates">The candidates, after every strategy has decided who is eligible.</param>
    /// <param name="nowUtc">The current time.</param>
    public static void Apply(QueueItem queueItem, IList<ActivityRoutingCandidate> candidates, DateTime nowUtc)
    {
        var declined = queueItem.DeclinedAgentIds;

        if (declined is null || declined.Count == 0)
        {
            return;
        }

        var eligible = candidates
            .Where(candidate => candidate.IsEligible)
            .Select(candidate => (Candidate: candidate, Position: declined.IndexOf(candidate.Agent.ItemId)))
            .ToArray();

        if (eligible.Length == 0)
        {
            return;
        }

        if (eligible.Any(entry => entry.Position < 0))
        {
            foreach (var (candidate, _) in eligible.Where(entry => entry.Position >= 0))
            {
                candidate.IsEligible = false;
                candidate.AddReason("Declined or missed this call, so it goes to an agent who has not been offered it yet.");
            }

            return;
        }

        // Everybody who could take the call has turned it down: another round, in the order they declined.
        var next = eligible.OrderBy(entry => entry.Position).First().Candidate;

        foreach (var (candidate, _) in eligible.Where(entry => !ReferenceEquals(entry.Candidate, next)))
        {
            candidate.IsEligible = false;
            candidate.AddReason("Declined this call more recently than the agent it is offered to next.");
        }

        // The next in line is the agent who declined last only when nobody else can take the call.
        if (string.Equals(next.Agent.ItemId, declined[^1], StringComparison.Ordinal) &&
            queueItem.LastDeclinedUtc is DateTime lastDeclinedUtc &&
            nowUtc - lastDeclinedUtc < ActivityRoutingService.DeclinedOfferRetryDelay)
        {
            next.IsEligible = false;
            next.AddReason("Declined this call moments ago, so it is not offered straight back to them.");
        }
    }
}
