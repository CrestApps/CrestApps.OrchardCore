using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;

/// <summary>
/// The default routed-distribution policy. Among the queue's members it keeps only the agents who are available
/// for SMS, hold every skill the queue requires, and are under their (capacity-weighted) concurrency budget, then
/// picks the one carrying the least load — so work spreads evenly and no eligible agent is skipped. Returns
/// <see langword="null"/> when nobody is eligible, letting the caller fall back to the shared pool.
/// </summary>
/// <remarks>
/// Skill matching reuses the same <see cref="ActivityQueue.RequiredSkills"/> configuration and the shared
/// <see cref="SkillTag"/> normalization that voice routing uses (see <c>RequiredSkillsRoutingStrategy</c>), so a
/// skill is defined once and honored identically across channels. Capacity is weighted across channels: an agent
/// currently on live voice interactions has proportionally less room for routed SMS, approximating the
/// single-engine capacity model without unifying the two routers.
/// </remarks>
public sealed class LeastLoadedSmsRoutingStrategy : ISmsRoutingStrategy
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly ISmsConversationStore _conversationStore;
    private readonly ISmsAgentAvailabilityService _availabilityService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IInteractionManager _interactionManager;

    // Each active live voice interaction consumes this many SMS-equivalent slots of an agent's concurrency budget,
    // so an agent on a call is meaningfully less available for routed SMS than an idle one.
    private readonly int _voiceCapacityWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeastLoadedSmsRoutingStrategy"/> class.
    /// </summary>
    public LeastLoadedSmsRoutingStrategy(
        IAgentProfileManager agentProfileManager,
        ISmsConversationStore conversationStore,
        ISmsAgentAvailabilityService availabilityService,
        IActivityQueueManager queueManager,
        IInteractionManager interactionManager,
        IOptions<SmsRoutedDistributionOptions> options)
    {
        _agentProfileManager = agentProfileManager;
        _conversationStore = conversationStore;
        _availabilityService = availabilityService;
        _queueManager = queueManager;
        _interactionManager = interactionManager;
        _voiceCapacityWeight = Math.Max(0, options.Value.VoiceCapacityWeight);
    }

    /// <inheritdoc/>
    public async Task<string> SelectAgentAsync(string queueId, string excludeAgentId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return null;
        }

        // Members are agents entitled to the queue — entitlement, not voice presence, because SMS reception is not
        // gated on being on a call. Resolved with a single indexed lookup rather than loading every agent.
        var agents = await _agentProfileManager.GetMembersForQueueAsync(queueId, cancellationToken);

        // The queue's required skills are shared with voice routing; normalize once for the whole selection.
        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);
        var requiredSkills = SkillTag.CreateAll(queue?.RequiredSkills ?? []);

        // Both load figures are read once for the whole queue rather than once per candidate: routing runs on
        // the inbound-message path, and a query per agent makes routing one message cost as much as the team is
        // large.
        var candidateIds = agents
            .Where(agent => !string.IsNullOrEmpty(agent.ItemId))
            .Select(agent => agent.ItemId)
            .ToArray();

        var smsLoads = await _conversationStore.CountOpenAssignedAsync(candidateIds, cancellationToken);
        var voiceLoads = await _interactionManager.CountActiveByAgentIdsAsync(candidateIds, cancellationToken);

        string bestAgentId = null;
        var bestLoad = int.MaxValue;

        foreach (var agent in agents)
        {
            if (string.IsNullOrEmpty(agent.ItemId) ||
                string.Equals(agent.ItemId, excludeAgentId, StringComparison.Ordinal))
            {
                continue;
            }

            // Derived: the agent both volunteered and still has a live session. Reading the flag alone pushed
            // conversations at agents who had closed the browser.
            if (!await _availabilityService.IsAvailableAsync(agent, cancellationToken))
            {
                continue;
            }

            var availability = _availabilityService.Get(agent);

            if (!HasRequiredSkills(agent, requiredSkills))
            {
                continue;
            }

            var smsLoad = smsLoads.GetValueOrDefault(agent.ItemId);

            // Capacity weighting: live voice work eats into the same concurrency budget.
            var activeVoice = voiceLoads.GetValueOrDefault(agent.ItemId);
            var load = smsLoad + (activeVoice * _voiceCapacityWeight);

            if (load >= availability.EffectiveMaxConcurrent)
            {
                continue;
            }

            // Least-loaded wins; ties break stably by agent id so selection is deterministic and testable.
            if (load < bestLoad ||
                (load == bestLoad && (bestAgentId is null || string.CompareOrdinal(agent.ItemId, bestAgentId) < 0)))
            {
                bestLoad = load;
                bestAgentId = agent.ItemId;
            }
        }

        return bestAgentId;
    }

    private static bool HasRequiredSkills(AgentProfile agent, IReadOnlyCollection<SkillTag> requiredSkills)
    {
        if (requiredSkills.Count == 0)
        {
            return true;
        }

        var agentSkills = new HashSet<SkillTag>(SkillTag.CreateAll(agent.Skills ?? []));

        return requiredSkills.All(agentSkills.Contains);
    }
}
