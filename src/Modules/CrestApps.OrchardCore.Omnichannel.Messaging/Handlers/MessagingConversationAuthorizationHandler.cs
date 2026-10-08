using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Handlers;

/// <summary>
/// Grants an operation on one conversation to a caller who does not hold
/// <see cref="MessagingPermissions.ViewAllConversations"/>, the way Orchard Core grants a content item to its owner.
/// A conversation is always authorized against <see cref="MessagingPermissions.ViewAllConversations"/>, which a supervisor
/// holds outright; this handler succeeds the requirement for anybody else whose conversation it is:
/// <list type="bullet">
///   <item>with <see cref="MessagingPermissions.ViewOwnConversations"/>, a conversation assigned to them, or one sent to
///   an endpoint they own that no colleague has claimed;</item>
///   <item>with <see cref="MessagingPermissions.ViewQueueConversations"/>, an unclaimed conversation of a queue they
///   serve, to read, claim or answer (a reply claims it).</item>
/// </list>
/// A conversation a colleague has claimed, and one no route gave to an agent or a queue, is never granted here. Queue
/// membership is confirmed against the agent entitlement policy, so the Agent Entitlements feature narrows messaging
/// the same way it narrows queue sign-in.
/// </summary>
internal sealed class MessagingConversationAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<IAuthorizationService> _authorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IAgentEntitlementPolicy _entitlementPolicy;

    /// <param name="authorizationService">
    /// The authorization service, resolved lazily: it is what runs this handler, so constructing it eagerly would close
    /// the cycle between them.
    /// </param>
    /// <param name="agentProfileManager">The agent profile manager used to resolve the caller's agent identity.</param>
    /// <param name="entitlementPolicy">The policy that decides which queues an agent may serve.</param>
    public MessagingConversationAuthorizationHandler(
        Lazy<IAuthorizationService> authorizationService,
        IAgentProfileManager agentProfileManager,
        IAgentEntitlementPolicy entitlementPolicy)
    {
        _authorizationService = authorizationService;
        _agentProfileManager = agentProfileManager;
        _entitlementPolicy = entitlementPolicy;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded ||
            requirement.Permission.Name != MessagingPermissions.ViewAllConversations.Name ||
            context.Resource is not ConversationAuthorizationResource { Conversation: not null } resource)
        {
            return;
        }

        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var agent = await _agentProfileManager.FindByUserIdAsync(userId);

        if (agent is null)
        {
            return;
        }

        var permission = GetGrantingPermission(agent, resource.Conversation, resource.Operation);

        if (permission is not null &&
            await _authorizationService.Value.AuthorizeAsync(context.User, permission))
        {
            context.Succeed(requirement);
        }
    }

    // The narrower permission that would grant the operation, or null when the conversation is not the caller's at all.
    private Permission GetGrantingPermission(AgentProfile agent, MessagingConversation conversation, ConversationOperation operation)
    {
        // Held by the caller: theirs, whichever queue it came from. An agent who has left the queue since, or was handed
        // it from a queue they never served, can still finish what they hold.
        if (IsSameAgent(conversation.AssignedAgentId, agent.ItemId))
        {
            return MessagingPermissions.ViewOwnConversations;
        }

        // A colleague holds it: claim-to-own means it is no longer anybody else's to read or answer.
        if (!string.IsNullOrEmpty(conversation.AssignedAgentId))
        {
            return null;
        }

        if (conversation.OwnerType == ConversationOwnerType.Queue)
        {
            // Nobody holds it yet, so a member may read it, claim it, or reply on it; the rest needs it claimed first.
            return (operation is ConversationOperation.View or ConversationOperation.Claim or ConversationOperation.Send) &&
                IsQueueMember(agent, conversation.OwnerId)
                    ? MessagingPermissions.ViewQueueConversations
                    : null;
        }

        // A personal conversation nobody holds is the endpoint owner's. One with no owner is a message no route claimed:
        // it waits for a supervisor to triage it, not for whichever agent opens it first.
        return IsSameAgent(conversation.OwnerId, agent.ItemId)
            ? MessagingPermissions.ViewOwnConversations
            : null;
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

    private static bool IsSameAgent(string candidate, string agentId)
        => !string.IsNullOrEmpty(candidate) &&
            !string.IsNullOrEmpty(agentId) &&
            string.Equals(candidate, agentId, StringComparison.OrdinalIgnoreCase);
}
