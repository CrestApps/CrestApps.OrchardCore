using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Aggregates MCP resources from all registered sources (catalog and file system skills).
/// </summary>
public interface IMcpServerResourceService
{
    /// <summary>
    /// Lists all resources from every registered source.
    /// </summary>
    /// <returns>A combined list of resources.</returns>
    Task<IList<Resource>> ListAsync();

    /// <summary>
    /// Reads a resource by URI from the first source that matches.
    /// </summary>
    /// <param name="request">The MCP request context containing the resource URI.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The read resource result.</returns>
    Task<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default);
}
