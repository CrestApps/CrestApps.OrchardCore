using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Keeps each agent's starred customers: the people they message most, shown first in their workspace.
/// </summary>
public interface IMessagingFavoritesService
{
    /// <summary>
    /// Gets the agent's starred customers, most recently starred first.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <returns>The starred customers; empty when there are none.</returns>
    IReadOnlyList<MessagingFavorite> GetFavorites(AgentProfile agent);

    /// <summary>
    /// Determines whether the agent has starred the customer behind a conversation.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="conversation">The conversation.</param>
    /// <returns><see langword="true"/> when the customer is starred.</returns>
    bool IsFavorite(AgentProfile agent, MessagingConversation conversation);

    /// <summary>
    /// Stars or unstars the customer behind a conversation for the agent.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="conversation">The conversation whose customer is starred.</param>
    /// <param name="displayName">The name the customer is shown by now.</param>
    /// <param name="favorite"><see langword="true"/> to star the customer, <see langword="false"/> to unstar them.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the list changed.</returns>
    Task<bool> SetFavoriteAsync(AgentProfile agent, MessagingConversation conversation, string displayName, bool favorite, CancellationToken cancellationToken = default);
}
