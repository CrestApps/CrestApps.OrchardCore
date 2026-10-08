using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Which conversations a user's lists show: the inbox, the count on the Messaging menu, and the notifications they are
/// sent. It is the list-wide counterpart of the per-conversation authorization handler, so a list never shows a
/// conversation the handler would refuse to open.
/// </summary>
/// <param name="IncludeAll">Whether every conversation is shown, which <see cref="MessagingPermissions.ViewAllConversations"/> grants.</param>
/// <param name="AgentId">The agent whose own conversations are shown, or <see langword="null"/> when none are.</param>
/// <param name="QueueIds">The queues whose unclaimed conversations are shown.</param>
internal sealed record MessagingInboxScope(bool IncludeAll, string AgentId, IReadOnlyCollection<string> QueueIds)
{
    /// <summary>
    /// Resolves the scope of the user's lists from their permissions, their agent profile and its entitlements.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="entitlementPolicy">The policy that decides which queues an agent may serve.</param>
    /// <param name="user">The user.</param>
    /// <param name="agent">The user's agent profile, or <see langword="null"/> when they have none.</param>
    /// <returns>The scope.</returns>
    public static async Task<MessagingInboxScope> ResolveAsync(
        IAuthorizationService authorizationService,
        IAgentEntitlementPolicy entitlementPolicy,
        ClaimsPrincipal user,
        AgentProfile agent)
    {
        var includeAll = await authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewAllConversations);

        if (agent is null || !await authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewOwnConversations))
        {
            return new MessagingInboxScope(includeAll, null, []);
        }

        // The queues the agent belongs to and the ones they may serve, as far as their entitlements allow, so an agent
        // whose queue was taken away stops seeing its customers in the list, the count and the notifications too.
        string[] queueIds = await authorizationService.AuthorizeAsync(user, MessagingPermissions.ViewQueueConversations)
            ? agent.QueueIds.Concat(agent.AllowedQueueIds)
                .Where(queueId => !string.IsNullOrEmpty(queueId) && entitlementPolicy.AllowsQueue(agent, queueId))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        return new MessagingInboxScope(includeAll, agent.ItemId, queueIds);
    }
}
