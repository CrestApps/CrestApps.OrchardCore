using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Services;

namespace CrestApps.OrchardCore.AI.Mcp.Core;

/// <summary>
/// Interface for the MCP resource store with additional lookup capabilities.
/// </summary>
public interface IMcpResourceStore : ISourceCatalog<McpResource>
{
    /// <summary>
    /// Retrieves a resource by its URI.
    /// </summary>
    /// <param name="uri">The URI of the resource to retrieve.</param>
    /// <returns>The resource if found; otherwise, <c>null</c>.</returns>
    ValueTask<McpResource> FindByUriAsync(string uri);
}
