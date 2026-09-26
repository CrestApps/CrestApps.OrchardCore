using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingConversationAuthorizationService"/>. A supervisor holding
/// <see cref="MessagingPermissions.ViewAllConversations"/> may do anything; every other caller is resolved to
/// an agent profile and may only act on the threads they own, are assigned, or serve through a queue they belong
/// to; the holder of a thread may also transfer it. Queue membership is confirmed against the agent entitlement policy, so the Agent Entitlements feature
/// narrows messaging access the same way it narrows queue sign-in.
/// </summary>
public sealed class MessagingConversationAuthorizationService : IMessagingConversationAuthorizationService
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationAuthorizationService"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service used for the supervisor permission check.</param>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the caller's agent identity.</param>
    /// <param name="entitlementPolicy">The policy that decides which queues an agent may serve.</param>
    public MessagingConversationAuthorizationService(
        IAuthorizationService authorizationService,
        IAgentProfileManager agentProfileManager,
        IAgentEntitlementPolicy entitlementPolicy)
    {
        _authorizationService = authorizationService;
        _agentProfileManager = agentProfileManager;
        _entitlementPolicy = entitlementPolicy;
    }

    /// <inheritdoc/>
    public async Task<bool> AuthorizeAsync(
        ClaimsPrincipal principal,
        MessagingConversation conversation,
        ConversationOperation operation,
        CancellationToken cancellationToken = default)
    {
        if (principal is null || conversation is null)
        {
            return false;
        }

        if (await _authorizationService.AuthorizeAsync(principal, MessagingPermissions.ViewAllConversations))
        {
            return true;
        }

        // Transfer needs no rule of its own: whoever holds a thread may hand it on, and the rules below only grant an
        // unclaimed thread the reading, claiming and replying that taking it involves, so an unclaimed thread has to be
        // claimed before it can be passed to somebody else.
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(userId, cancellationToken);

        if (agent is null)
        {
            return false;
        }

        return conversation.OwnerType == ConversationOwnerType.Queue
            ? AuthorizeQueueConversation(agent, conversation, operation)
            : AuthorizePersonalConversation(agent, conversation, operation);
    }

    private static bool AuthorizePersonalConversation(
        AgentProfile agent,
        MessagingConversation conversation,
        ConversationOperation operation)
    {
        if (IsSameAgent(conversation.OwnerId, agent.ItemId) || IsSameAgent(conversation.AssignedAgentId, agent.ItemId))
        {
            return operation != ConversationOperation.Claim || IsClaimable(conversation);
        }

        // Nobody owns the thread yet, so any workspace agent may read it, claim it, or reply on it (a reply claims).
        if (IsClaimable(conversation) && string.IsNullOrEmpty(conversation.OwnerId))
        {
            return operation is ConversationOperation.View
                or ConversationOperation.Claim
                or ConversationOperation.Send;
        }

        return false;
    }

    private bool AuthorizeQueueConversation(
        AgentProfile agent,
        MessagingConversation conversation,
        ConversationOperation operation)
    {
        if (!IsQueueMember(agent, conversation.OwnerId))
        {
            return false;
        }

        if (IsSameAgent(conversation.AssignedAgentId, agent.ItemId))
        {
            return true;
        }

        // The thread belongs to a colleague in the same department: claim-to-own means it is no longer theirs to
        // read or answer.
        if (!string.IsNullOrEmpty(conversation.AssignedAgentId))
        {
            return false;
        }

        // Nobody holds the thread yet, so any member may read it, claim it, or reply on it (a reply claims it).
        return operation is ConversationOperation.View
            or ConversationOperation.Claim
            or ConversationOperation.Send;
    }

    private bool IsQueueMember(AgentProfile agent, string queueId)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return false;
        }

        var belongs = agent.QueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true ||
            agent.AllowedQueueIds?.Contains(queueId, StringComparer.OrdinalIgnoreCase) == true;

        return belongs && _entitlementPolicy.AllowsQueue(agent, queueId);
    }

    private static bool IsClaimable(MessagingConversation conversation)
        => conversation.AssignmentStatus != ConversationAssignmentStatus.Assigned ||
            string.IsNullOrEmpty(conversation.AssignedAgentId);

    private static bool IsSameAgent(string candidate, string agentId)
        => !string.IsNullOrEmpty(candidate) &&
            !string.IsNullOrEmpty(agentId) &&
            string.Equals(candidate, agentId, StringComparison.OrdinalIgnoreCase);
}
