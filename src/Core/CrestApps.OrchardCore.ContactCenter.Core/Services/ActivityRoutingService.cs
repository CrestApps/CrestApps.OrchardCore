using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Applies registered routing strategies and returns an explainable routing decision.
/// </summary>
public sealed class ActivityRoutingService : IActivityRoutingService
{
    /// <summary>
    /// How long the only agent able to take an item must wait, after declining it, before it is offered to them again.
    /// </summary>
    public static readonly TimeSpan DeclinedOfferRetryDelay = TimeSpan.FromMinutes(1);

    private readonly IEnumerable<IActivityRoutingStrategy> _strategies;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityRoutingService"/> class.
    /// </summary>
    /// <param name="strategies">The routing strategies to apply.</param>
    /// <param name="clock">The clock a declined offer's retry delay is measured against.</param>
    public ActivityRoutingService(IEnumerable<IActivityRoutingStrategy> strategies, IClock clock)
    {
        _strategies = strategies;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<ActivityRoutingDecision> SelectAgentAsync(
        ActivityQueue queue,
        QueueItem queueItem,
        IEnumerable<AgentAvailability> availability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(queueItem);

        var candidates = availability?
            .Where(entry => entry?.Agent is not null)
            .Select(entry => new ActivityRoutingCandidate(entry))
            .ToList() ?? [];
        var context = new ActivityRoutingContext(queue, queueItem, candidates);

        // Not a preference a strategy weighs: an agent who sent this call away is never handed it back, however
        // long they have been idle or however sticky the queue is. The strategies only boost eligible candidates.
        foreach (var candidate in candidates.Where(candidate => IsExcluded(queueItem, candidate.Agent)))
        {
            candidate.IsEligible = false;
            candidate.AddReason("Transferred this call away, so it is not offered back to them.");
        }

        foreach (var strategy in _strategies.OrderBy(strategy => strategy.Order))
        {
            await strategy.ApplyAsync(context, cancellationToken);
        }

        // After the strategies, so a round of declines is judged against who can actually take the call, and so a
        // sticky or longest-idle boost cannot put an agent who just turned the call down back at the front.
        DeclinedOfferRouting.Apply(queueItem, candidates, _clock.UtcNow);

        if (candidates.Count == 0)
        {
            return CreateNoMatchDecision(queue, queueItem, candidates, "No agents are currently available for this queue.");
        }

        var selected = candidates
            .Where(candidate => candidate.IsEligible)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Agent.PresenceChangedUtc ?? DateTime.MaxValue)
            .FirstOrDefault();

        if (selected is null)
        {
            return CreateNoMatchDecision(queue, queueItem, candidates, "No available agent matched the queue routing policy.");
        }

        return new ActivityRoutingDecision
        {
            Succeeded = true,
            Queue = queue,
            QueueItem = queueItem,
            Agent = selected.Agent,
            Reason = "Selected the highest-scoring eligible agent.",
            Candidates = candidates,
        };
    }

    /// <summary>
    /// Whether the queue item must never be offered to the agent.
    /// </summary>
    /// <param name="queueItem">The queue item.</param>
    /// <param name="agentId">The agent profile identifier.</param>
    internal static bool IsExcluded(QueueItem queueItem, string agentId)
        => !string.IsNullOrEmpty(agentId) &&
            queueItem?.ExcludedAgentIds?.Contains(agentId, StringComparer.Ordinal) == true;

    private static bool IsExcluded(QueueItem queueItem, AgentProfile agent)
        => IsExcluded(queueItem, agent?.ItemId);

    private static ActivityRoutingDecision CreateNoMatchDecision(
        ActivityQueue queue,
        QueueItem queueItem,
        IList<ActivityRoutingCandidate> candidates,
        string reason)
    {
        return new ActivityRoutingDecision
        {
            Succeeded = false,
            Queue = queue,
            QueueItem = queueItem,
            Reason = reason,
            Candidates = candidates,
        };
    }
}
