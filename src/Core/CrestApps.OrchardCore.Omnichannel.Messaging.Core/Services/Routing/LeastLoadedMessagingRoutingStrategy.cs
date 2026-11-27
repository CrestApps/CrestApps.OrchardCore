using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;

/// <summary>
/// The default routed-distribution policy. Among the queue's members it keeps only the agents who are available
/// for messaging, hold every skill the queue requires, and are under their (capacity-weighted) concurrency budget, then
/// picks the one carrying the least load — so work spreads evenly and no eligible agent is skipped. Returns
/// <see langword="null"/> when nobody is eligible, letting the caller fall back to the shared pool.
/// </summary>
/// <remarks>
/// Skill matching reuses the same <see cref="ActivityQueue.RequiredSkills"/> configuration and the shared
/// <see cref="SkillTag"/> normalization that voice routing uses (see <c>RequiredSkillsRoutingStrategy</c>), so a
/// skill is defined once and honored identically across channels. Capacity is weighted across channels: an agent
/// currently on live voice interactions has proportionally less room for routed conversations, approximating the
/// single-engine capacity model without unifying the two routers.
/// </remarks>
public sealed class LeastLoadedMessagingRoutingStrategy : IMessagingRoutingStrategy
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly MessagingAgentCapacity _capacity;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeastLoadedMessagingRoutingStrategy"/> class.
    /// </summary>
    /// <param name="agentProfileManager">The agent profiles, read for the queue's members.</param>
    /// <param name="conversationStore">The conversations, counted for each agent's open assigned threads.</param>
    /// <param name="availabilityService">The agents' messaging availability and limits.</param>
    /// <param name="queueManager">The queues, read for their required skills.</param>
    /// <param name="interactionManager">The interactions, counted for each agent's live calls.</param>
    /// <param name="options">The routed distribution options, read for how much a live call weighs.</param>
    public LeastLoadedMessagingRoutingStrategy(
        IAgentProfileManager agentProfileManager,
        IMessagingConversationStore conversationStore,
        IMessagingAvailabilityService availabilityService,
        IActivityQueueManager queueManager,
        IInteractionManager interactionManager,
        IOptions<MessagingRoutedDistributionOptions> options)
    {
        _agentProfileManager = agentProfileManager;
        _queueManager = queueManager;

        // Each active live voice interaction consumes this many conversation-equivalent slots of an agent's concurrency
        // budget, so an agent on a call is meaningfully less available for routed conversations than an idle one.
        _capacity = new MessagingAgentCapacity(conversationStore, availabilityService, interactionManager, options.Value.VoiceCapacityWeight);
    }

    /// <inheritdoc/>
    public async Task<string> SelectAgentAsync(string queueId, string excludeAgentId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return null;
        }

        // Members are agents entitled to the queue — entitlement, not voice presence, because messaging reception is not
        // gated on being on a call. Resolved with a single indexed lookup rather than loading every agent.
        var agents = await _agentProfileManager.GetMembersForQueueAsync(queueId, cancellationToken);

        // The queue's required skills are shared with voice routing; normalize once for the whole selection.
        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var requiredSkills = SkillTag.CreateAll(queue?.RequiredSkills ?? []);

        var loads = await _capacity.MeasureAsync(agents, cancellationToken);

        string bestAgentId = null;
        var bestLoad = int.MaxValue;

        foreach (var load in loads)
        {
            if (string.Equals(load.Agent.ItemId, excludeAgentId, StringComparison.Ordinal) ||
                !load.IsAvailable ||
                !MessagingAgentCapacity.HasRequiredSkills(load.Agent, requiredSkills) ||
                !load.HasCapacity)
            {
                continue;
            }

            // Least-loaded wins; ties break stably by agent id so selection is deterministic and testable.
            if (load.WeightedLoad < bestLoad ||
                (load.WeightedLoad == bestLoad && (bestAgentId is null || string.CompareOrdinal(load.Agent.ItemId, bestAgentId) < 0)))
            {
                bestLoad = load.WeightedLoad;
                bestAgentId = load.Agent.ItemId;
            }
        }

        return bestAgentId;
    }
}
