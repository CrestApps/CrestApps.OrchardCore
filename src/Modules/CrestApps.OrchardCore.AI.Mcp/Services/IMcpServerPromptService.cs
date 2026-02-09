using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

/// <summary>
/// Aggregates MCP prompts from all registered sources (catalog and file system skills).
/// </summary>
public interface IMcpServerPromptService
{
    /// <summary>
    /// Lists all prompts from every registered source.
    /// </summary>
    /// <returns>A combined list of prompts.</returns>
    Task<IList<Prompt>> ListAsync();

    /// <summary>
    /// Gets a prompt by name from the first source that contains it.
    /// </summary>
    /// <param name="request">The MCP request context containing the prompt name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The prompt result.</returns>
    Task<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default);
}
