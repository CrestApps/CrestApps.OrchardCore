using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Applies registered routing strategies and returns an explainable routing decision.
/// </summary>
public sealed class ActivityRoutingService : IActivityRoutingService
{
    private readonly IEnumerable<IActivityRoutingStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityRoutingService"/> class.
    /// </summary>
    /// <param name="strategies">The routing strategies to apply.</param>
    public ActivityRoutingService(IEnumerable<IActivityRoutingStrategy> strategies)
    {
        _strategies = strategies;
    }

    /// <inheritdoc/>
    public async Task<ActivityRoutingDecision> SelectAgentAsync(
        ActivityQueue queue,
        QueueItem queueItem,
        IEnumerable<AgentProfile> agents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(queueItem);

        var candidates = agents?.Select(agent => new ActivityRoutingCandidate(agent)).ToList() ?? [];
        var context = new ActivityRoutingContext(queue, queueItem, candidates);

        foreach (var strategy in _strategies.OrderBy(strategy => strategy.Order))
        {
            await strategy.ApplyAsync(context, cancellationToken);
        }

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
