using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Picks the queue holding the contact who most deserves to be answered, across every queue the agent serves.
/// The head item of each queue is read in one grouped query, scored with the same effective priority the queue
/// itself routes on (so SLA aging counts), then ordered by membership priority, effective priority, and finally
/// how long the contact has waited.
/// </summary>
public sealed class AgentWorkSelector : IAgentWorkSelector
{
    private readonly IQueueItemStore _queueItemStore;
    private readonly IActivityQueueManager _queueManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkSelector"/> class.
    /// </summary>
    public AgentWorkSelector(
        IQueueItemStore queueItemStore,
        IActivityQueueManager queueManager,
        IClock clock)
    {
        _queueItemStore = queueItemStore;
        _queueManager = queueManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<string> SelectNextForAgentAsync(AgentProfile agent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(agent);

        var memberships = ResolveMemberships(agent);

        if (memberships.Count == 0)
        {
            return null;
        }

        // One grouped query for every queue the agent serves, rather than one per queue.
        var heads = await _queueItemStore.GetHeadWaitingByQueueAsync(memberships.Keys, cancellationToken);

        if (heads.Count == 0)
        {
            return null;
        }

        var now = _clock.UtcNow;
        string bestQueueId = null;
        var bestMembershipPriority = int.MaxValue;
        var bestEffectivePriority = int.MinValue;
        var bestEnqueuedUtc = DateTime.MaxValue;

        foreach (var head in heads)
        {
            if (head is null || string.IsNullOrEmpty(head.QueueId) || !memberships.TryGetValue(head.QueueId, out var membership))
            {
                continue;
            }

            // A delay means this agent is the backup for the queue: the item is not theirs to take until the
            // agents without a delay have had their chance at it.
            if (membership.DelaySeconds > 0 && (now - head.EnqueuedUtc).TotalSeconds < membership.DelaySeconds)
            {
                continue;
            }

            var queue = await _queueManager.FindByIdAsync(head.QueueId, cancellationToken);

            if (queue is null)
            {
                continue;
            }

            var effectivePriority = QueueItemPrioritizer.GetEffectivePriority(head, queue, now);

            if (!IsBetter(
                membership.Priority,
                effectivePriority,
                head.EnqueuedUtc,
                bestMembershipPriority,
                bestEffectivePriority,
                bestEnqueuedUtc))
            {
                continue;
            }

            bestQueueId = head.QueueId;
            bestMembershipPriority = membership.Priority;
            bestEffectivePriority = effectivePriority;
            bestEnqueuedUtc = head.EnqueuedUtc;
        }

        return bestQueueId;
    }

    private static bool IsBetter(
        int membershipPriority,
        int effectivePriority,
        DateTime enqueuedUtc,
        int bestMembershipPriority,
        int bestEffectivePriority,
        DateTime bestEnqueuedUtc)
    {
        if (membershipPriority != bestMembershipPriority)
        {
            return membershipPriority < bestMembershipPriority;
        }

        if (effectivePriority != bestEffectivePriority)
        {
            return effectivePriority > bestEffectivePriority;
        }

        return enqueuedUtc < bestEnqueuedUtc;
    }

    /// <summary>
    /// Builds the membership map, falling back to the bare queue list for agents signed in before memberships
    /// existed. Ignoring that list would sign every existing agent out of every queue on upgrade.
    /// </summary>
    private static Dictionary<string, AgentQueueMembership> ResolveMemberships(AgentProfile agent)
    {
        var memberships = new Dictionary<string, AgentQueueMembership>(StringComparer.OrdinalIgnoreCase);

        foreach (var membership in agent.QueueMemberships)
        {
            if (!string.IsNullOrWhiteSpace(membership?.QueueId))
            {
                memberships[membership.QueueId] = membership;
            }
        }

        foreach (var queueId in agent.QueueIds)
        {
            if (!string.IsNullOrWhiteSpace(queueId) && !memberships.ContainsKey(queueId))
            {
                memberships[queueId] = new AgentQueueMembership { QueueId = queueId };
            }
        }

        return memberships;
    }
}
