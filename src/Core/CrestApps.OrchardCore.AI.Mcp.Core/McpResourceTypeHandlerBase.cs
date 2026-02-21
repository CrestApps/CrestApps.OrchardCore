using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Base class for MCP resource type handlers that provides common logic.
/// Subclasses only need to implement the <see cref="GetResultAsync(McpResource, IReadOnlyDictionary{string, string}, CancellationToken)"/> method.
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
    public Task<ReadResourceResult> ReadAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(variables);

        return GetResultAsync(resource, variables, cancellationToken);
    }

    /// <summary>
    /// Reads the resource content using the extracted URI variables.
    /// </summary>
    /// <param name="resource">The MCP resource definition.</param>
    /// <param name="variables">The variables extracted from the URI pattern match.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task containing the read resource result.</returns>
    protected abstract Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken);

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

    /// <summary>
    /// Determines whether the given MIME type represents text-based content
    /// that can be safely read as a string.
    /// </summary>
    /// <param name="mimeType">The MIME type to check.</param>
    /// <returns><c>true</c> if the MIME type is text-based; otherwise, <c>false</c>.</returns>
    protected static bool IsTextMimeType(string mimeType)
    {
        if (mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mimeType.EndsWith("/json", StringComparison.OrdinalIgnoreCase)
            || mimeType.EndsWith("+json", StringComparison.OrdinalIgnoreCase)
            || mimeType.EndsWith("/xml", StringComparison.OrdinalIgnoreCase)
            || mimeType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase)
            || mimeType.Equals("application/javascript", StringComparison.OrdinalIgnoreCase)
            || mimeType.Equals("application/ecmascript", StringComparison.OrdinalIgnoreCase)
            || mimeType.Equals("image/svg+xml", StringComparison.OrdinalIgnoreCase);
    }
}
