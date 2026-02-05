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
    /// Gets the URI patterns this strategy can handle.
    /// </summary>
    string[] UriPatterns { get; }

    /// <summary>
    /// Determines whether this strategy can handle the given URI.
    /// </summary>
    /// <param name="uri">The parsed URI to check.</param>
    /// <returns>True if this strategy can handle the URI; otherwise, false.</returns>
    bool CanHandle(Uri uri);

    /// <summary>
    /// Reads the resource content for the given URI.
    /// </summary>
    /// <param name="resource">The MCP resource definition.</param>
    /// <param name="uri">The parsed URI.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resource content result.</returns>
    Task<ReadResourceResult> ReadAsync(McpResource resource, Uri uri, CancellationToken cancellationToken = default);
}
