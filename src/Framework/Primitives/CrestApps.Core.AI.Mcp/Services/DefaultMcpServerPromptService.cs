using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Services;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.Core.AI.Mcp.Services;

public sealed class DefaultMcpServerPromptService : IMcpServerPromptService
{
    private readonly INamedCatalog<McpPrompt> _catalog;
    private readonly IEnumerable<McpServerPrompt> _sdkPrompts;

    public DefaultMcpServerPromptService(
        INamedCatalog<McpPrompt> catalog,
        IEnumerable<McpServerPrompt> sdkPrompts = null)
    {
        _catalog = catalog;
        _sdkPrompts = sdkPrompts ?? [];
    }

    public async Task<IList<Prompt>> ListAsync()
    {
        var prompts = (await _catalog.GetAllAsync())
            .Where(entry => entry.Prompt != null)
            .Select(entry => entry.Prompt)
            .ToList();

        foreach (var sdkPrompt in _sdkPrompts)
        {
            if (!prompts.Any(prompt => prompt.Name == sdkPrompt.ProtocolPrompt.Name))
            {
                prompts.Add(sdkPrompt.ProtocolPrompt);
            }
        }

        return prompts;
    }

    public async Task<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken = default)
    {
        var entry = (await _catalog.GetAllAsync()).FirstOrDefault(entry => entry.Prompt?.Name == request.Params.Name);

        if (entry?.Prompt is not null)
        {
            return new GetPromptResult
            {
                Description = entry.Prompt.Description,
                Messages = [],
            };
        }

        var sdkPrompt = _sdkPrompts.FirstOrDefault(prompt => prompt.ProtocolPrompt.Name == request.Params.Name);

        if (sdkPrompt is not null)
        {
            return await sdkPrompt.GetAsync(request, cancellationToken);
        }

        throw new McpException($"Prompt '{request.Params.Name}' not found.");
    }
}
