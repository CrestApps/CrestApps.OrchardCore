using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Resolves which external capabilities are semantically relevant to a user prompt
/// before intent detection runs. Implementations perform lightweight semantic matching
/// (e.g., embedding-based cosine similarity) to provide the intent detector with
/// contextual information about available capabilities.
/// </summary>
/// <remarks>
/// This interface is intentionally generic and not tied to any specific capability
/// provider (e.g., MCP). The MCP module provides an implementation that uses
/// MCP server metadata for resolution.
/// </remarks>
public interface IPreIntentCapabilityResolver
{
    /// <summary>
    /// Resolves capabilities relevant to the user's prompt.
    /// </summary>
    /// <param name="routingContext">The prompt routing context containing user input and provider info.</param>
    /// <param name="completionContext">The AI completion context containing connection and capability source IDs.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A <see cref="PreIntentResolutionContext"/> containing matched capability summaries,
    /// or <see cref="PreIntentResolutionContext.Empty"/> if no relevant capabilities are found.
    /// </returns>
    Task<PreIntentResolutionContext> ResolveAsync(
        PromptRoutingContext routingContext,
        AICompletionContext completionContext,
        CancellationToken cancellationToken = default);
}
