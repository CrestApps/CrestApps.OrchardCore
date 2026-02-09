using CrestApps.OrchardCore.AI.Mcp.Core.Models;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Provides cached metadata about MCP server capabilities.
/// Queries MCP servers for available tools, prompts, and resources, and caches the results.
/// </summary>
public interface IMcpServerMetadataProvider
{
    /// <summary>
    /// Gets the capabilities of the specified MCP server connection.
    /// Results are cached with a configurable TTL.
    /// </summary>
    /// <param name="connection">The MCP connection to query.</param>
    /// <returns>The server's capabilities, or <c>null</c> if the connection cannot be resolved.</returns>
    Task<McpServerCapabilities> GetCapabilitiesAsync(McpConnection connection);

    /// <summary>
    /// Invalidates the cached metadata for a specific connection.
    /// </summary>
    /// <param name="connectionId">The connection identifier to invalidate.</param>
    Task InvalidateAsync(string connectionId);

    /// <summary>
    /// Invalidates all cached MCP server metadata.
    /// </summary>
    Task InvalidateAllAsync();
}
