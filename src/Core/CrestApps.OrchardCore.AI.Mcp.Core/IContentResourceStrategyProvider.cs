using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Provides a strategy for handling content:// URIs.
/// Implement this interface to add support for additional content URI patterns.
/// </summary>
public interface IContentResourceStrategyProvider
{
    /// <summary>
    /// Gets the URI path patterns this strategy can handle.
    /// </summary>
    string[] UriPatterns { get; }

    /// <summary>
    /// Determines whether this strategy can handle the given resource URI.
    /// </summary>
    /// <param name="uri">The parsed resource URI.</param>
    /// <returns>True if this strategy can handle the URI; otherwise, false.</returns>
    bool CanHandle(McpResourceUri uri);

    /// <summary>
    /// Reads the resource content for the given URI.
    /// </summary>
    /// <param name="resource">The MCP resource definition.</param>
    /// <param name="uri">The parsed resource URI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resource content result.</returns>
    Task<ReadResourceResult> ReadAsync(McpResource resource, McpResourceUri uri, CancellationToken cancellationToken = default);
}
