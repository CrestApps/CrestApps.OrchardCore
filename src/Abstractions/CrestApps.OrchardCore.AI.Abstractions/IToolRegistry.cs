using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// A unified index of all available tools (local and MCP) that supports retrieval
/// and relevance-based searching for tool scoping during orchestration.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets all tool entries scoped to the given completion context.
    /// </summary>
    Task<IReadOnlyList<ToolRegistryEntry>> GetAllAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for the most relevant tools based on a capability query string.
    /// Returns the top-K entries ranked by relevance.
    /// </summary>
    /// <param name="query">A capability description or user intent to match against tool metadata.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="context">The completion context that scopes available tools.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A ranked list of the most relevant tool entries.</returns>
    Task<IReadOnlyList<ToolRegistryEntry>> SearchAsync(
        string query,
        int topK,
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
