using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Base class for MCP resource type handlers that provides common URI parsing logic.
/// Subclasses only need to implement the <see cref="GetResultAsync(McpResource, McpResourceUri, CancellationToken)"/> method.
/// </summary>
public abstract class McpResourceTypeHandlerBase : IMcpResourceTypeHandler
{
    protected McpResourceTypeHandlerBase(string type)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);

        Type = type;
    }

    /// <inheritdoc/>
    public string Type { get; }

    /// <inheritdoc/>
    public Task<ReadResourceResult> ReadAsync(McpResource resource, McpResourceUri resourceUri, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(resourceUri);

        ArgumentException.ThrowIfNullOrEmpty(resourceUri?.Uri);

        return GetResultAsync(resource, resourceUri, cancellationToken);
    }

    /// <summary>
    /// Reads the resource content using the parsed URI.
    /// </summary>
    /// <param name="resource">The MCP resource definition.</param>
    /// <param name="resourceUri">The parsed resource URI.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task containing the read resource result.</returns>
    protected abstract Task<ReadResourceResult> GetResultAsync(McpResource resource, McpResourceUri resourceUri, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a <see cref="ReadResourceResult"/> containing an error message instead of throwing an exception.
    /// </summary>
    /// <param name="uri">The resource URI to include in the response.</param>
    /// <param name="errorMessage">The error message to return to the caller.</param>
    /// <returns>A <see cref="ReadResourceResult"/> with the error message as text content.</returns>
    public static ReadResourceResult CreateErrorResult(string uri, string errorMessage)
    {
        return new ReadResourceResult
        {
            Contents =
            [
                new TextResourceContents
                {
                    Uri = uri,
                    MimeType = "text/plain",
                    Text = errorMessage,
                }
            ]
        };
    }
}
