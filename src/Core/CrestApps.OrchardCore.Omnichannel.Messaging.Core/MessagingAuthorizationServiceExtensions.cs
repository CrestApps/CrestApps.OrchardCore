using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core;

/// <summary>
/// Authorizes an operation on one messaging conversation.
/// </summary>
public static class MessagingAuthorizationServiceExtensions
{
    /// <summary>
    /// Determines whether the user may perform the operation on the conversation. The conversation is authorized
    /// against <see cref="MessagingPermissions.ViewAllConversations"/>, which a supervisor holds outright; the messaging
    /// conversation authorization handler grants anybody else the conversations that are theirs to work.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="user">The user.</param>
    /// <param name="conversation">The conversation the user wants to act on.</param>
    /// <param name="operation">The operation the user wants to perform.</param>
    /// <returns><see langword="true"/> when the operation is allowed.</returns>
    public static Task<bool> AuthorizeConversationAsync(
        this IAuthorizationService authorizationService,
        ClaimsPrincipal user,
        MessagingConversation conversation,
        ConversationOperation operation)
    {
        ArgumentNullException.ThrowIfNull(authorizationService);

        if (user is null || conversation is null)
        {
            return Task.FromResult(false);
        }

        return authorizationService.AuthorizeAsync(
            user,
            MessagingPermissions.ViewAllConversations,
            new ConversationAuthorizationResource(conversation, operation));
    }
}
