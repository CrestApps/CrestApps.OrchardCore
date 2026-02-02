using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Core;

/// <summary>
/// Represents a store for chat interaction prompts.
/// </summary>
public interface IChatInteractionPromptStore : ICatalog<ChatInteractionPrompt>
{
    /// <summary>
    /// Gets all prompts for a specific chat interaction, ordered by creation time.
    /// </summary>
    /// <param name="chatInteractionId">The chat interaction ID.</param>
    /// <returns>A collection of prompts ordered by CreatedUtc.</returns>
    Task<IReadOnlyCollection<ChatInteractionPrompt>> GetPromptsAsync(string chatInteractionId);

    /// <summary>
    /// Deletes all prompts for a specific chat interaction.
    /// </summary>
    /// <param name="chatInteractionId">The chat interaction ID.</param>
    /// <returns>The number of prompts deleted.</returns>
    Task<int> DeleteAllPromptsAsync(string chatInteractionId);
}
