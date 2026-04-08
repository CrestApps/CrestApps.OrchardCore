using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.Core.AI.Mcp.Services;

/// <summary>
/// Manages server-side MCP resources, providing listing of resources and
/// resource templates, and reading resource content exposed by the MCP server endpoint.
/// </summary>
public interface IMcpServerResourceService
{
    /// <summary>
    /// Asynchronously lists all resources registered on the MCP server.
    /// </summary>
    /// <returns>A list of available MCP resources.</returns>
    Task<IList<Resource>> ListAsync();

    /// <summary>
    /// Asynchronously lists all resource templates registered on the MCP server.
    /// </summary>
    /// <returns>A list of available MCP resource templates.</returns>
    Task<IList<ResourceTemplate>> ListTemplatesAsync();

    /// <summary>
    /// Asynchronously reads the content of a specific resource.
    /// </summary>
    /// <param name="request">The request context containing the resource URI to read.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The resource content result.</returns>
    Task<ReadResourceResult> ReadAsync(RequestContext<ReadResourceRequestParams> request, CancellationToken cancellationToken = default);
}
