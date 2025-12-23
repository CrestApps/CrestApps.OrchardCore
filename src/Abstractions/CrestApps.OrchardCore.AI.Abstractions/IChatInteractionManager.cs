using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Manages chat interactions lifecycle and persistence.
/// </summary>
public interface IChatInteractionManager
{
    /// <summary>
    /// Asynchronously retrieves an existing chat interaction by its ID for the current user.
    /// </summary>
    /// <param name="itemId">The unique identifier of the chat interaction.</param>
    /// <returns>The <see cref="ChatInteraction"/> if found and owned by the current user, or <c>null</c>.</returns>
    ValueTask<ChatInteraction> FindAsync(string itemId);

    /// <summary>
    /// Asynchronously retrieves a paginated list of chat interactions for the current user.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="context">The query context for filtering.</param>
    /// <returns>A <see cref="PageResult{ChatInteraction}"/> containing the interactions and total count.</returns>
    ValueTask<PageResult<ChatInteraction>> PageAsync(int page, int pageSize, ChatInteractionQueryContext context);

    /// <summary>
    /// Asynchronously creates a new chat interaction with the specified source.
    /// </summary>
    /// <param name="source">The source/provider name for the interaction.</param>
    /// <returns>A new <see cref="ChatInteraction"/> instance.</returns>
    ValueTask<ChatInteraction> NewAsync(string source);

    /// <summary>
    /// Asynchronously creates the specified chat interaction in the catalog.
    /// </summary>
    /// <param name="interaction">The chat interaction to create.</param>
    ValueTask CreateAsync(ChatInteraction interaction);

    /// <summary>
    /// Asynchronously updates the specified chat interaction.
    /// </summary>
    /// <param name="interaction">The chat interaction to update.</param>
    ValueTask UpdateAsync(ChatInteraction interaction);

    /// <summary>
    /// Asynchronously deletes the specified chat interaction for the current user.
    /// </summary>
    /// <param name="itemId">The unique identifier of the chat interaction to delete.</param>
    /// <returns><c>true</c> if the interaction was deleted, <c>false</c> otherwise.</returns>
    ValueTask<bool> DeleteAsync(string itemId);

    /// <summary>
    /// Asynchronously deletes all chat interactions for the current user.
    /// </summary>
    /// <returns>The number of interactions deleted.</returns>
    ValueTask<int> DeleteAllAsync();
}
