using CrestApps.OrchardCore.AI.Mcp.Core.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Resolves which MCP capabilities are semantically relevant to a user prompt
/// using in-memory vector similarity search over cached capability embeddings.
/// This runs before intent detection to provide the intent model with relevant
/// capability context for more accurate routing decisions.
/// </summary>
public interface IMcpCapabilityResolver
{
    /// <summary>
    /// Resolves MCP capabilities that are semantically relevant to the given user prompt.
    /// </summary>
    /// <param name="prompt">The user's input prompt.</param>
    /// <param name="providerName">The AI provider name for embedding generation.</param>
    /// <param name="connectionName">The AI connection name for embedding generation.</param>
    /// <param name="mcpConnectionIds">The MCP connection IDs configured for the current profile.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// A <see cref="McpCapabilityResolutionResult"/> containing the top matching capabilities,
    /// or an empty result if no capabilities match above the configured threshold.
    /// </returns>
    Task<McpCapabilityResolutionResult> ResolveAsync(
        string prompt,
        string providerName,
        string connectionName,
        string[] mcpConnectionIds,
        CancellationToken cancellationToken = default);
}
