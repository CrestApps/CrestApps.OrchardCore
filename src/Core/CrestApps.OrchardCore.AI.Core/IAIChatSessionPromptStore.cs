using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// Represents a store for AI chat session prompts.
/// </summary>
public interface IAIChatSessionPromptStore : ICatalog<AIChatSessionPrompt>
{
    /// <summary>
    /// Gets all prompts for a specific session, ordered by creation time.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>A collection of prompts ordered by CreatedUtc.</returns>
    Task<IReadOnlyList<AIChatSessionPrompt>> GetPromptsAsync(string sessionId);

    /// <summary>
    /// Deletes all prompts for a specific session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>The number of prompts deleted.</returns>
    Task<int> DeleteAllPromptsAsync(string sessionId);

    /// <summary>
    /// Counts the number of prompts for a specific session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <returns>The number of prompts.</returns>
    Task<int> CountAsync(string sessionId);
}
