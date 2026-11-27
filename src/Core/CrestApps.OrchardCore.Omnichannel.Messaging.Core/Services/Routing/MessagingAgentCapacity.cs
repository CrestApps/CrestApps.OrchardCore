using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;

/// <summary>
/// Measures how much messaging work agents hold and how much more they can take, the one capacity rule both routed
/// distribution and the routing rules apply: open assigned conversations plus live calls, a call weighing as
/// <c>voiceCapacityWeight</c> conversations, against the agent's messaging limit.
/// </summary>
internal sealed class MessagingAgentCapacity
{
    private readonly IMessagingConversationStore _conversationStore;
    private readonly IMessagingAvailabilityService _availabilityService;
    private readonly IInteractionManager _interactionManager;
    private readonly int _voiceCapacityWeight;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingAgentCapacity"/> class.
    /// </summary>
    /// <param name="conversationStore">The conversations, counted for each agent's open assigned threads.</param>
    /// <param name="availabilityService">The agents' messaging availability and limits.</param>
    /// <param name="interactionManager">The interactions, counted for each agent's live calls.</param>
    /// <param name="voiceCapacityWeight">How many conversations one live call weighs.</param>
    public MessagingAgentCapacity(
        IMessagingConversationStore conversationStore,
        IMessagingAvailabilityService availabilityService,
        IInteractionManager interactionManager,
        int voiceCapacityWeight)
    {
        _conversationStore = conversationStore;
        _availabilityService = availabilityService;
        _interactionManager = interactionManager;
        _voiceCapacityWeight = Math.Max(0, voiceCapacityWeight);
    }

    /// <summary>
    /// Measures agents. Both load figures are read once for all of them rather than once per agent: routing runs on the
    /// inbound-message path, and a query per agent makes routing one message cost as much as the team is large.
    /// </summary>
    /// <param name="agents">The agents.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Each agent's load, availability and limit, in the order given.</returns>
    public async Task<IReadOnlyList<MessagingAgentLoad>> MeasureAsync(IEnumerable<AgentProfile> agents, CancellationToken cancellationToken = default)
    {
        var profiles = agents?
            .Where(agent => !string.IsNullOrEmpty(agent?.ItemId))
            .GroupBy(agent => agent.ItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray() ?? [];

        if (profiles.Length == 0)
        {
            return [];
        }

        var ids = profiles.Select(agent => agent.ItemId).ToArray();
        var conversationLoads = await _conversationStore.CountOpenAssignedAsync(ids, cancellationToken);
        var voiceLoads = await _interactionManager.CountActiveByAgentIdsAsync(ids, cancellationToken);
        var loads = new List<MessagingAgentLoad>(profiles.Length);

        foreach (var agent in profiles)
        {
            var openConversations = conversationLoads.GetValueOrDefault(agent.ItemId);
            var activeCalls = voiceLoads.GetValueOrDefault(agent.ItemId);

            loads.Add(new MessagingAgentLoad(
                agent,
                openConversations,
                activeCalls,
                openConversations + (activeCalls * _voiceCapacityWeight),
                _availabilityService.Get(agent).EffectiveMaxConcurrent,
                // Derived: the agent both volunteered and still has a live session. Reading the flag alone pushed
                // conversations at agents who had closed the browser.
                await _availabilityService.IsAvailableAsync(agent, cancellationToken)));
        }

        return loads;
    }

    /// <summary>
    /// Determines whether an agent holds every skill a queue requires, with the shared <see cref="SkillTag"/> rule voice
    /// routing uses.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="requiredSkills">The queue's required skills, normalized.</param>
    /// <returns><see langword="true"/> when the agent holds them all.</returns>
    public static bool HasRequiredSkills(AgentProfile agent, IReadOnlyCollection<SkillTag> requiredSkills)
    {
        if (requiredSkills is null || requiredSkills.Count == 0)
        {
            return true;
        }

        var agentSkills = new HashSet<SkillTag>(SkillTag.CreateAll(agent.Skills ?? []));

        return requiredSkills.All(agentSkills.Contains);
    }
}

/// <summary>
/// How much messaging work an agent holds and how much they can hold.
/// </summary>
/// <param name="Agent">The agent.</param>
/// <param name="OpenConversations">The open conversations assigned to the agent.</param>
/// <param name="ActiveCalls">The agent's live calls.</param>
/// <param name="WeightedLoad">The open conversations plus the live calls, each call weighing several conversations.</param>
/// <param name="MaxConcurrent">The most conversations the agent's messaging limit allows.</param>
/// <param name="IsAvailable">Whether the agent is available for messaging now and has a live workspace session.</param>
internal sealed record MessagingAgentLoad(
    AgentProfile Agent,
    int OpenConversations,
    int ActiveCalls,
    int WeightedLoad,
    int MaxConcurrent,
    bool IsAvailable)
{
    /// <summary>
    /// Gets whether the agent has room for another conversation.
    /// </summary>
    public bool HasCapacity => WeightedLoad < MaxConcurrent;
}
