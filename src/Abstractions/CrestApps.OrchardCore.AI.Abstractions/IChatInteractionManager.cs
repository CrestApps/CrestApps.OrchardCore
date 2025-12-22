using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Manages chat interactions lifecycle and persistence.
/// </summary>
public interface IChatInteractionManager
{
    /// <summary>
    /// Asynchronously retrieves an existing chat interaction by its ID.
    /// </summary>
    /// <param name="interactionId">The unique identifier of the chat interaction.</param>
    /// <returns>The <see cref="ChatInteraction"/> if found, or <c>null</c> if not found.</returns>
    Task<ChatInteraction> FindAsync(string interactionId);

    /// <summary>
    /// Asynchronously retrieves a paginated list of chat interactions.
    /// </summary>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="context">The query context for filtering.</param>
    /// <returns>A <see cref="ChatInteractionResult"/> containing the interactions and total count.</returns>
    Task<ChatInteractionResult> PageAsync(int page, int pageSize, ChatInteractionQueryContext context);

    /// <summary>
    /// Asynchronously creates a new chat interaction.
    /// </summary>
    /// <returns>A new <see cref="ChatInteraction"/> instance.</returns>
    Task<ChatInteraction> NewAsync();

    /// <summary>
    /// Asynchronously saves or updates the specified chat interaction.
    /// </summary>
    /// <param name="interaction">The chat interaction to save.</param>
    Task SaveAsync(ChatInteraction interaction);

    /// <summary>
    /// Asynchronously deletes the specified chat interaction.
    /// </summary>
    /// <param name="interactionId">The unique identifier of the chat interaction to delete.</param>
    /// <returns><c>true</c> if the interaction was deleted, <c>false</c> otherwise.</returns>
    Task<bool> DeleteAsync(string interactionId);

    /// <summary>
    /// Asynchronously deletes all chat interactions for the current user.
    /// </summary>
    /// <returns>The number of interactions deleted.</returns>
    Task<int> DeleteAllAsync();
}
