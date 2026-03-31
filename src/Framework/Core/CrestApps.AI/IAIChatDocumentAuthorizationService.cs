using System.Security.Claims;
using CrestApps.AI.Models;

namespace CrestApps.AI;

/// <summary>
/// Authorizes document upload and removal operations for chat interactions and chat sessions.
/// </summary>
public interface IAIChatDocumentAuthorizationService
{
    /// <summary>
    /// Determines whether the specified user can manage documents for the supplied chat interaction.
    /// </summary>
    /// <param name="user">The current user principal.</param>
    /// <param name="interaction">The chat interaction being modified.</param>
    /// <returns><see langword="true"/> when the user may upload or remove documents for the interaction.</returns>
    Task<bool> CanManageChatInteractionDocumentsAsync(ClaimsPrincipal user, ChatInteraction interaction);

    /// <summary>
    /// Determines whether the specified user can manage documents for the supplied chat session.
    /// </summary>
    /// <param name="user">The current user principal.</param>
    /// <param name="profile">The AI profile associated with the session.</param>
    /// <param name="session">The chat session being modified.</param>
    /// <returns><see langword="true"/> when the user may upload or remove documents for the session.</returns>
    Task<bool> CanManageChatSessionDocumentsAsync(ClaimsPrincipal user, AIProfile profile, AIChatSession session);
}
