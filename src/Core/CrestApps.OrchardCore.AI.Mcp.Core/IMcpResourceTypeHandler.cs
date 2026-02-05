using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Defines a handler for reading MCP resource content based on its type.
/// Each resource type (e.g., File, FTP, SQL) should have its own implementation.
/// </summary>
public interface IMcpResourceTypeHandler
{
    /// <summary>
    /// Gets the type of resource this handler supports.
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Reads the resource content and returns the result.
    /// </summary>
    /// <param name="resource">The MCP resource definition.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task containing the read resource result.</returns>
    Task<ReadResourceResult> ReadAsync(McpResource resource, CancellationToken cancellationToken = default);
}
