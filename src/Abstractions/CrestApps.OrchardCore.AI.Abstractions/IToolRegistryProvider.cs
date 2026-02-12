using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Provides tool metadata entries to the unified tool registry.
/// Implementations supply tools from different sources (local registrations, MCP servers, etc.).
/// </summary>
public interface IToolRegistryProvider
{
    /// <summary>
    /// Retrieves all tool entries available from this provider, scoped to the given context.
    /// </summary>
    /// <param name="context">The completion context containing configured tool names,
    /// instance IDs, and MCP connection IDs that scope the returned tools.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A read-only list of tool registry entries from this provider.</returns>
    Task<IReadOnlyList<ToolRegistryEntry>> GetToolsAsync(
        AICompletionContext context,
        CancellationToken cancellationToken = default);
}
