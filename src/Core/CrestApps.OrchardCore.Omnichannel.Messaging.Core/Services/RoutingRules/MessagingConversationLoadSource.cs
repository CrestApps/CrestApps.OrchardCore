using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;

/// <summary>
/// The kinds of messaging work team assignment balances.
/// </summary>
internal static class MessagingWorkKinds
{
    /// <summary>
    /// Open conversations assigned to the agent.
    /// </summary>
    public const string Conversation = "MessagingConversation";

    /// <summary>
    /// Open conversations assigned to the agent plus their live calls, each call weighing several conversations.
    /// </summary>
    public const string WeightedConversation = "MessagingConversationWeighted";
}

/// <summary>
/// The agents' messaging loads a routing rule has just measured, so the load sources team assignment reads report the
/// same figures the rule filtered the agents with instead of counting them a second time.
/// </summary>
internal sealed class MessagingRoutingLoadSnapshot
{
    private Dictionary<string, MessagingAgentLoad> _loads = new(StringComparer.Ordinal);

    /// <summary>
    /// Keeps the loads, by the agents' user ids.
    /// </summary>
    /// <param name="loads">The loads.</param>
    public void Set(IEnumerable<MessagingAgentLoad> loads)
    {
        _loads = (loads ?? [])
            .Where(load => !string.IsNullOrEmpty(load.Agent.UserId))
            .GroupBy(load => load.Agent.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets the load of a user, when it was measured.
    /// </summary>
    /// <param name="userId">The user id.</param>
    /// <param name="load">The load.</param>
    /// <returns><see langword="true"/> when the user's load was measured.</returns>
    public bool TryGet(string userId, out MessagingAgentLoad load)
        => _loads.TryGetValue(userId, out load);
}

/// <summary>
/// Reports each agent's messaging work to team assignment: how many open conversations they hold (or, for the weighted
/// kind, those plus their live calls) and when a routing rule last gave them one.
/// </summary>
internal abstract class MessagingConversationLoadSourceBase : ITeamMemberLoadSource
{
    private readonly MessagingRoutingLoadSnapshot _snapshot;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly MessagingAgentCapacity _capacity;
    private readonly IMessagingRoutingLedger _ledger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationLoadSourceBase"/> class.
    /// </summary>
    /// <param name="snapshot">The loads the current routing rule measured.</param>
    /// <param name="agentProfileManager">The agent profiles, read for users the rule did not measure.</param>
    /// <param name="capacity">The capacity rule.</param>
    /// <param name="ledger">When each agent was last given a conversation.</param>
    protected MessagingConversationLoadSourceBase(
        MessagingRoutingLoadSnapshot snapshot,
        IAgentProfileManager agentProfileManager,
        MessagingAgentCapacity capacity,
        IMessagingRoutingLedger ledger)
    {
        _snapshot = snapshot;
        _agentProfileManager = agentProfileManager;
        _capacity = capacity;
        _ledger = ledger;
    }

    /// <inheritdoc/>
    public abstract string WorkKind { get; }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, TeamMemberLoad>> GetLoadAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var result = new Dictionary<string, TeamMemberLoad>(StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return result;
        }

        var measured = new Dictionary<string, MessagingAgentLoad>(StringComparer.Ordinal);
        var unmeasured = new List<string>();

        foreach (var id in ids)
        {
            if (_snapshot.TryGet(id, out var load))
            {
                measured[id] = load;
            }
            else
            {
                unmeasured.Add(id);
            }
        }

        // A caller other than a routing rule asks about users nobody measured: their agent profiles are read and measured
        // together.
        if (unmeasured.Count > 0)
        {
            var agents = new List<AgentProfile>();

            foreach (var id in unmeasured)
            {
                if (await _agentProfileManager.FindByUserIdAsync(id, cancellationToken) is { } agent)
                {
                    agents.Add(agent);
                }
            }

            foreach (var load in await _capacity.MeasureAsync(agents, cancellationToken))
            {
                measured[load.Agent.UserId] = load;
            }
        }

        var lastAssigned = await _ledger.GetLastAssignedAsync(ids, cancellationToken);

        foreach (var id in ids)
        {
            result[id] = new TeamMemberLoad
            {
                Open = measured.TryGetValue(id, out var load) ? GetOpen(load) : 0,
                LastAssignedUtc = lastAssigned.TryGetValue(id, out var assignedUtc) ? assignedUtc : null,
            };
        }

        return result;
    }

    /// <summary>
    /// Gets the figure balanced for an agent.
    /// </summary>
    /// <param name="load">The agent's load.</param>
    /// <returns>The figure.</returns>
    protected abstract int GetOpen(MessagingAgentLoad load);
}

/// <summary>
/// Reports each agent's open assigned conversations.
/// </summary>
internal sealed class MessagingConversationLoadSource : MessagingConversationLoadSourceBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationLoadSource"/> class.
    /// </summary>
    /// <param name="snapshot">The loads the current routing rule measured.</param>
    /// <param name="agentProfileManager">The agent profiles.</param>
    /// <param name="conversationStore">The conversations.</param>
    /// <param name="availabilityService">The agents' messaging availability.</param>
    /// <param name="interactionManager">The interactions.</param>
    /// <param name="options">The routed distribution options.</param>
    /// <param name="ledger">When each agent was last given a conversation.</param>
    public MessagingConversationLoadSource(
        MessagingRoutingLoadSnapshot snapshot,
        IAgentProfileManager agentProfileManager,
        IMessagingConversationStore conversationStore,
        IMessagingAvailabilityService availabilityService,
        IInteractionManager interactionManager,
        IOptions<MessagingRoutedDistributionOptions> options,
        IMessagingRoutingLedger ledger)
        : base(
            snapshot,
            agentProfileManager,
            new MessagingAgentCapacity(conversationStore, availabilityService, interactionManager, options.Value.VoiceCapacityWeight),
            ledger)
    {
    }

    /// <inheritdoc/>
    public override string WorkKind => MessagingWorkKinds.Conversation;

    /// <inheritdoc/>
    protected override int GetOpen(MessagingAgentLoad load)
        => load.OpenConversations;
}

/// <summary>
/// Reports each agent's open assigned conversations plus their live calls, each call weighing several conversations.
/// </summary>
internal sealed class MessagingWeightedConversationLoadSource : MessagingConversationLoadSourceBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingWeightedConversationLoadSource"/> class.
    /// </summary>
    /// <param name="snapshot">The loads the current routing rule measured.</param>
    /// <param name="agentProfileManager">The agent profiles.</param>
    /// <param name="conversationStore">The conversations.</param>
    /// <param name="availabilityService">The agents' messaging availability.</param>
    /// <param name="interactionManager">The interactions.</param>
    /// <param name="options">The routed distribution options, read for how much a live call weighs.</param>
    /// <param name="ledger">When each agent was last given a conversation.</param>
    public MessagingWeightedConversationLoadSource(
        MessagingRoutingLoadSnapshot snapshot,
        IAgentProfileManager agentProfileManager,
        IMessagingConversationStore conversationStore,
        IMessagingAvailabilityService availabilityService,
        IInteractionManager interactionManager,
        IOptions<MessagingRoutedDistributionOptions> options,
        IMessagingRoutingLedger ledger)
        : base(
            snapshot,
            agentProfileManager,
            new MessagingAgentCapacity(conversationStore, availabilityService, interactionManager, options.Value.VoiceCapacityWeight),
            ledger)
    {
    }

    /// <inheritdoc/>
    public override string WorkKind => MessagingWorkKinds.WeightedConversation;

    /// <inheritdoc/>
    protected override int GetOpen(MessagingAgentLoad load)
        => load.WeightedLoad;
}
