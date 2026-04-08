using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;

namespace CrestApps.Core.AI.Chat;

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
