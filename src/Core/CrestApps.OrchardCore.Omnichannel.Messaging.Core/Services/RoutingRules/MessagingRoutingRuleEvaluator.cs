using CrestApps.Core.Services;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;

/// <summary>
/// The default <see cref="IMessagingRoutingRuleEvaluator"/>. It keeps the rules that apply to the conversation, finds
/// each one's candidates, drops those who are away or at their limit, and lets team assignment pick one by the rule's
/// strategy, so the round robin and least-loaded rules are the same ones every other assignment in the solution uses.
/// </summary>
internal sealed class MessagingRoutingRuleEvaluator : IMessagingRoutingRuleEvaluator
{
    private readonly ICatalog<MessagingRoutingRule> _rules;
    private readonly MessagingRoutingCandidateResolver _candidateResolver;
    private readonly MessagingAgentCapacity _capacity;
    private readonly MessagingRoutingLoadSnapshot _snapshot;
    private readonly ITeamAssignmentService _assignmentService;
    private readonly IMessagingRoutingLedger _ledger;
    private readonly IMessagingAgentNameProvider _agentNames;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingRoutingRuleEvaluator"/> class.
    /// </summary>
    /// <param name="rules">The routing rules.</param>
    /// <param name="candidateResolver">Finds each rule's candidates.</param>
    /// <param name="conversationStore">The conversations, counted for each agent's open threads.</param>
    /// <param name="availabilityService">The agents' messaging availability and limits.</param>
    /// <param name="interactionManager">The interactions, counted for each agent's live calls.</param>
    /// <param name="options">The routed distribution options, read for how much a live call weighs.</param>
    /// <param name="snapshot">Where the measured loads are kept for the load sources team assignment reads.</param>
    /// <param name="assignmentService">The team assignment service that picks the agent.</param>
    /// <param name="ledger">When each agent was last given a conversation.</param>
    /// <param name="agentNames">The agents' names, recorded in the conversation's history.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public MessagingRoutingRuleEvaluator(
        ICatalog<MessagingRoutingRule> rules,
        MessagingRoutingCandidateResolver candidateResolver,
        IMessagingConversationStore conversationStore,
        IMessagingAvailabilityService availabilityService,
        IInteractionManager interactionManager,
        IOptions<MessagingRoutedDistributionOptions> options,
        MessagingRoutingLoadSnapshot snapshot,
        ITeamAssignmentService assignmentService,
        IMessagingRoutingLedger ledger,
        IMessagingAgentNameProvider agentNames,
        IClock clock,
        ILogger<MessagingRoutingRuleEvaluator> logger)
    {
        _rules = rules;
        _candidateResolver = candidateResolver;
        _capacity = new MessagingAgentCapacity(conversationStore, availabilityService, interactionManager, options.Value.VoiceCapacityWeight);
        _snapshot = snapshot;
        _assignmentService = assignmentService;
        _ledger = ledger;
        _agentNames = agentNames;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MessagingRoutingRuleMatch> AssignAsync(MessagingRoutingRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Conversation);

        if (request.Trigger == MessagingRoutingRuleTriggers.None)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var rules = (await _rules.GetAllAsync(cancellationToken))
            .Where(rule => MessagingRoutingRuleMatcher.Applies(rule, request, now))
            .OrderBy(rule => rule.Order)
            .ThenBy(rule => rule.DisplayText, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var rule in rules)
        {
            var match = await TryRuleAsync(rule, request, cancellationToken);

            if (match is null)
            {
                continue;
            }

            await ApplyAsync(request, match, now, cancellationToken);

            return match;
        }

        return null;
    }

    private async Task<MessagingRoutingRuleMatch> TryRuleAsync(MessagingRoutingRule rule, MessagingRoutingRuleRequest request, CancellationToken cancellationToken)
    {
        var candidates = await _candidateResolver.ResolveAsync(rule, request.QueueId, cancellationToken);

        if (candidates.Agents.Count == 0)
        {
            LogNoAgent(rule, request, "it has no candidates");

            return null;
        }

        var loads = await _capacity.MeasureAsync(candidates.Agents, cancellationToken);

        var eligible = loads
            .Where(load => !string.IsNullOrEmpty(load.Agent.UserId) &&
                (!rule.OnlyAvailableAgents || load.IsAvailable) &&
                (!rule.RespectMaxConcurrent || load.HasCapacity) &&
                MessagingAgentCapacity.HasRequiredSkills(load.Agent, candidates.RequiredSkills))
            .ToArray();

        if (eligible.Length == 0)
        {
            LogNoAgent(rule, request, "none of its candidates is available with room for another conversation");

            return null;
        }

        _snapshot.Set(eligible);

        var picked = await _assignmentService.PickAsync(new TeamAssignmentRequest
        {
            CandidateUserIds = eligible.Select(load => load.Agent.UserId).ToArray(),
            WorkKind = rule.Strategy == MessagingRoutingRuleStrategies.LeastLoaded
                ? MessagingWorkKinds.WeightedConversation
                : MessagingWorkKinds.Conversation,
            Strategy = rule.Strategy == MessagingRoutingRuleStrategies.RoundRobin
                ? TeamAssignmentStrategies.RoundRobin
                : TeamAssignmentStrategies.LeastLoaded,

            // Messaging availability is its own (a live workspace session and the agent's own switch), and it was
            // applied above; a source about another kind of work, such as being signed in to a phone, does not apply.
            OnlyAvailable = false,
        }, cancellationToken);

        var userId = picked.Member?.UserId;

        if (string.IsNullOrEmpty(userId))
        {
            LogNoAgent(rule, request, $"team assignment picked nobody ({picked.Outcome})");

            return null;
        }

        var agent = eligible.First(load => string.Equals(load.Agent.UserId, userId, StringComparison.Ordinal)).Agent;

        return new MessagingRoutingRuleMatch(rule, agent.ItemId, agent.UserId);
    }

    private async Task ApplyAsync(MessagingRoutingRuleRequest request, MessagingRoutingRuleMatch match, DateTime now, CancellationToken cancellationToken)
    {
        var conversation = request.Conversation;
        var previousAgentId = conversation.AssignedAgentId;

        // A queue's conversation stays the queue's, so supervisors still see it under the department and an agent who
        // does not pick it up loses it to the queue again; one that belongs to no queue becomes the agent's own.
        if (!string.IsNullOrEmpty(request.QueueId))
        {
            conversation.OwnerType = ConversationOwnerType.Queue;
            conversation.OwnerId = request.QueueId;
        }
        else
        {
            conversation.OwnerType = ConversationOwnerType.Personal;
            conversation.OwnerId = match.AgentId;
        }

        conversation.AssignedAgentId = match.AgentId;
        conversation.AssignmentStatus = ConversationAssignmentStatus.Assigned;
        conversation.AssignedUtc = now;
        conversation.ReassignmentAttempts = 0;

        conversation.History ??= [];
        conversation.History.Add(new MessagingConversationEvent
        {
            Kind = MessagingConversationEventKind.Routed,
            OccurredUtc = now,
            FromAgentId = string.IsNullOrEmpty(previousAgentId) ? null : previousAgentId,
            ToAgentId = match.AgentId,
            ToName = await _agentNames.GetDisplayNameAsync(match.AgentId, cancellationToken),
            RuleName = match.Rule.DisplayText,
        });

        while (conversation.History.Count > MessagingConversationTransferService.MaxHistoryEntries)
        {
            conversation.History.RemoveAt(0);
        }

        await _ledger.RecordAsync(match.UserId, now, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Routing rule '{Rule}' assigned conversation {ConversationId} to agent {AgentId} ({Trigger}).",
                match.Rule.DisplayText.SanitizeLogValue(),
                conversation.ItemId.SanitizeLogValue(),
                match.AgentId.SanitizeLogValue(),
                request.Trigger);
        }
    }

    private void LogNoAgent(MessagingRoutingRule rule, MessagingRoutingRuleRequest request, string reason)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Routing rule '{Rule}' did not assign conversation {ConversationId} ({Trigger}): {Reason}.",
                rule.DisplayText.SanitizeLogValue(),
                request.Conversation.ItemId.SanitizeLogValue(),
                request.Trigger,
                reason);
        }
    }
}

/// <summary>
/// Decides whether a routing rule applies to a conversation at a moment.
/// </summary>
internal static class MessagingRoutingRuleMatcher
{
    /// <summary>
    /// Determines whether a rule applies.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="request">The conversation and the moment.</param>
    /// <param name="nowUtc">The current time, which a <see cref="MessagingRoutingRuleTriggers.Waiting"/> rule's wait is measured to.</param>
    /// <returns><see langword="true"/> when the rule is enabled, applies at the moment and takes the conversation.</returns>
    public static bool Applies(MessagingRoutingRule rule, MessagingRoutingRuleRequest request, DateTime nowUtc)
    {
        if (rule is null || !rule.Enabled || !rule.Triggers.HasFlag(request.Trigger))
        {
            return false;
        }

        var conversation = request.Conversation;

        if (request.Trigger == MessagingRoutingRuleTriggers.Waiting &&
            (conversation.LastMessageUtc is null ||
            conversation.LastMessageUtc > nowUtc.AddMinutes(-Math.Max(1, rule.WaitingMinutes))))
        {
            return false;
        }

        return Takes(rule.Channels, conversation.Channel) &&
            Takes(rule.ServiceAddresses, conversation.ServiceAddress) &&
            Takes(rule.QueueIds, request.QueueId) &&
            Takes(rule.CampaignIds, conversation.CampaignId);
    }

    private static bool Takes(IList<string> values, string value)
    {
        if (values is null || !values.Any(entry => !string.IsNullOrWhiteSpace(entry)))
        {
            return true;
        }

        return !string.IsNullOrEmpty(value) &&
            values.Any(entry => string.Equals(entry?.Trim(), value, StringComparison.OrdinalIgnoreCase));
    }
}
