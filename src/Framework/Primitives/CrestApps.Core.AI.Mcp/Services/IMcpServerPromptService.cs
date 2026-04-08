using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.Core.AI.Mcp.Services;

/// <summary>
/// Manages server-side MCP prompts, providing listing and retrieval
/// of prompts exposed by the MCP server endpoint.
/// </summary>
public interface IMcpServerPromptService
{
    /// <summary>
    /// Asynchronously lists all prompts registered on the MCP server.
    /// </summary>
    /// <returns>A list of available MCP prompts.</returns>
    Task<IList<Prompt>> ListAsync();

    /// <summary>
    /// Asynchronously retrieves and evaluates a specific prompt by its parameters.
    /// </summary>
    /// <param name="request">The request context containing the prompt parameters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The evaluated prompt result containing messages to return to the client.</returns>
    Task<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default);
}
