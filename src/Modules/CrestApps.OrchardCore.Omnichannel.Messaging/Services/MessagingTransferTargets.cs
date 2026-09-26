using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Lists where a conversation can be transferred: the other people who can work messaging conversations, and the
/// teams (queues) whose shared pool it can be sent back to. The same rule decides what the picker offers and what the
/// transfer accepts, so the picker never offers somebody the transfer then refuses.
/// </summary>
public sealed class MessagingTransferTargets
{
    /// <summary>
    /// The most options one search returns.
    /// </summary>
    public const int MaxResults = 50;

    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly UserManager<IUser> _userManager;
    private readonly IUserClaimsPrincipalFactory<IUser> _principalFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayNameProvider _displayNameProvider;

    public MessagingTransferTargets(
        IAgentProfileManager agentProfileManager,
        IEnumerable<IActivityQueueManager> queueManagers,
        UserManager<IUser> userManager,
        IUserClaimsPrincipalFactory<IUser> principalFactory,
        IAuthorizationService authorizationService,
        IDisplayNameProvider displayNameProvider)
    {
        _agentProfileManager = agentProfileManager;
        // Queues are a feature of their own that the workspace does not require: without it there is no team to send
        // a conversation back to, only people.
        _queueManager = queueManagers.FirstOrDefault();
        _userManager = userManager;
        _principalFactory = principalFactory;
        _authorizationService = authorizationService;
        _displayNameProvider = displayNameProvider;
    }

    /// <summary>
    /// Gets a value indicating whether a conversation can be sent back to a team.
    /// </summary>
    public bool SupportsQueues => _queueManager is not null;

    /// <summary>
    /// Lists the people the conversation can go to, by name, leaving out whoever holds it now.
    /// </summary>
    /// <param name="conversation">The conversation being transferred.</param>
    /// <param name="query">Text the name must contain, or empty for everybody.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options, ordered by name.</returns>
    public async Task<IReadOnlyList<MessagingTransferTarget>> SearchAgentsAsync(MessagingConversation conversation, string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        var holderId = conversation.GetHolderAgentId();
        var term = query?.Trim();
        var targets = new List<MessagingTransferTarget>();

        foreach (var agent in await _agentProfileManager.GetAllAsync(cancellationToken))
        {
            if (agent is null ||
                string.IsNullOrEmpty(agent.ItemId) ||
                string.Equals(agent.ItemId, holderId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var user = await GetEligibleUserAsync(agent);

            // Somebody the picker cannot name is left out rather than shown as an identifier.
            var name = user is null ? null : await GetNameAsync(agent, user, cancellationToken);

            if (name is null ||
                (!string.IsNullOrEmpty(term) && !name.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            {
                continue;
            }

            targets.Add(new MessagingTransferTarget(agent.ItemId, name));
        }

        return targets
            .OrderBy(target => target.Text, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxResults)
            .ToArray();
    }

    /// <summary>
    /// Lists the teams the conversation can be sent back to, by name, leaving out the team whose shared pool it
    /// already waits in.
    /// </summary>
    /// <param name="conversation">The conversation being transferred.</param>
    /// <param name="query">Text the name must contain, or empty for every team.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The options, ordered by name; empty when queues are not available.</returns>
    public async Task<IReadOnlyList<MessagingTransferTarget>> SearchQueuesAsync(MessagingConversation conversation, string query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (_queueManager is null)
        {
            return [];
        }

        var waitingInQueueId = conversation.OwnerType == ConversationOwnerType.Queue && conversation.GetHolderAgentId() is null
            ? conversation.OwnerId
            : null;
        var term = query?.Trim();

        return (await _queueManager.GetEnabledAsync(cancellationToken))
            .Where(queue => queue is not null &&
                queue.Enabled &&
                !string.IsNullOrWhiteSpace(queue.Name) &&
                !ContactCenterConstants.IsDirectRoutingQueue(queue.ItemId) &&
                !string.Equals(queue.ItemId, waitingInQueueId, StringComparison.Ordinal) &&
                (string.IsNullOrEmpty(term) || queue.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxResults)
            .Select(queue => new MessagingTransferTarget(queue.ItemId, queue.Name))
            .ToArray();
    }

    /// <summary>
    /// Determines whether a conversation may be transferred to an agent: the person behind the profile must be able
    /// to use the messaging workspace, or they could never open what they were sent.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the agent can receive the conversation.</returns>
    public async Task<bool> IsEligibleAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(agentId))
        {
            return false;
        }

        var agent = await _agentProfileManager.FindByIdAsync(agentId.Trim(), cancellationToken);

        return agent is not null && await GetEligibleUserAsync(agent) is not null;
    }

    // The user behind an agent profile, or null when there is none or they cannot use the messaging workspace.
    private async Task<IUser> GetEligibleUserAsync(AgentProfile agent)
    {
        if (string.IsNullOrEmpty(agent.UserId))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(agent.UserId);

        if (user is null)
        {
            return null;
        }

        var principal = await _principalFactory.CreateAsync(user);

        return await _authorizationService.AuthorizeAsync(principal, MessagingPermissions.UseMessagingWorkspace)
            ? user
            : null;
    }

    private async Task<string> GetNameAsync(AgentProfile agent, IUser user, CancellationToken cancellationToken)
    {
        var name = await _displayNameProvider.GetAsync(user, cancellationToken);

        return !string.IsNullOrWhiteSpace(name)
            ? name
            : MessagingAgentNameProvider.GetProfileLabel(agent) ?? user.UserName;
    }
}

/// <summary>
/// One place a conversation can be transferred to, as the searchable picker reads it.
/// </summary>
/// <param name="Value">The agent profile or queue identifier.</param>
/// <param name="Text">The name shown for it.</param>
public sealed record MessagingTransferTarget(string Value, string Text);
