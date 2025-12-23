using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Catalog interface for ChatInteraction with user-scoped operations.
/// </summary>
public interface IChatInteractionCatalog : ISourceCatalog<ChatInteraction>
{
    /// <summary>
    /// Asynchronously retrieves a chat interaction by ID for the current user.
    /// </summary>
    /// <param name="itemId">The unique identifier of the chat interaction.</param>
    /// <returns>The <see cref="ChatInteraction"/> if found and owned by the current user, or <c>null</c>.</returns>
    ValueTask<ChatInteraction> FindByIdForUserAsync(string itemId);

    /// <summary>
    /// Asynchronously retrieves a paginated list of chat interactions for the current user.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="context">The query context for filtering.</param>
    /// <returns>A <see cref="PageResult{ChatInteraction}"/> containing the interactions and total count.</returns>
    ValueTask<PageResult<ChatInteraction>> PageForUserAsync(int page, int pageSize, ChatInteractionQueryContext context);

    /// <summary>
    /// Asynchronously deletes a chat interaction by ID for the current user.
    /// </summary>
    /// <param name="itemId">The unique identifier of the chat interaction to delete.</param>
    /// <returns><c>true</c> if the interaction was deleted, <c>false</c> otherwise.</returns>
    ValueTask<bool> DeleteForUserAsync(string itemId);

    /// <summary>
    /// Asynchronously deletes all chat interactions for the current user.
    /// </summary>
    /// <returns>The number of interactions deleted.</returns>
    ValueTask<int> DeleteAllForUserAsync();
}
