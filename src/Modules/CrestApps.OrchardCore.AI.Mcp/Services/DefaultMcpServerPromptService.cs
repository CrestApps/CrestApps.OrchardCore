using CrestApps.AgentSkills.Mcp.Abstractions;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.Services;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CrestApps.OrchardCore.AI.Mcp.Services;

public sealed class DefaultMcpServerPromptService : IMcpServerPromptService
{
    private readonly INamedCatalogManager<McpPrompt> _catalogManager;
    private readonly IMcpPromptProvider _skillPromptProvider;
    private readonly IEnumerable<McpServerPrompt> _sdkPrompts;

    public DefaultMcpServerPromptService(
        INamedCatalogManager<McpPrompt> catalogManager,
        IMcpPromptProvider skillPromptProvider,
        IEnumerable<McpServerPrompt> sdkPrompts)
    {
        _catalogManager = catalogManager;
        _skillPromptProvider = skillPromptProvider;
        _sdkPrompts = sdkPrompts;
    }

    public async Task<IList<Prompt>> ListAsync()
    {
        var entries = await _catalogManager.GetAllAsync();

        var prompts = entries
            .Where(e => e.Prompt != null)
            .Select(e => e.Prompt)
            .ToList();

        var skillPrompts = await _skillPromptProvider.GetPromptsAsync();

        foreach (var skillPrompt in skillPrompts)
        {
            prompts.Add(skillPrompt.ProtocolPrompt);
        }

        // Include prompts registered via the MCP C# SDK.
        foreach (var sdkPrompt in _sdkPrompts)
        {
            if (!prompts.Any(p => p.Name == sdkPrompt.ProtocolPrompt.Name))
            {
                prompts.Add(sdkPrompt.ProtocolPrompt);
            }
        }

        return prompts;
    }

    public async Task<GetPromptResult> GetAsync(RequestContext<GetPromptRequestParams> request, CancellationToken cancellationToken)
    {
        var entries = await _catalogManager.GetAllAsync();
        var entry = entries.FirstOrDefault(e => e.Prompt?.Name == request.Params.Name);

        if (entry?.Prompt is not null)
        {
            return new GetPromptResult
            {
                Description = entry.Prompt.Description,
                Messages = [],
            };
        }

        var skillPrompts = await _skillPromptProvider.GetPromptsAsync();
        var skillPrompt = skillPrompts.FirstOrDefault(p => p.ProtocolPrompt.Name == request.Params.Name);

        if (skillPrompt is not null)
        {
            return await skillPrompt.GetAsync(request, cancellationToken);
        }

        // Try prompts registered via the MCP C# SDK.
        var sdkPrompt = _sdkPrompts.FirstOrDefault(p => p.ProtocolPrompt.Name == request.Params.Name);

        if (sdkPrompt is not null)
        {
            return await sdkPrompt.GetAsync(request, cancellationToken);
        }

        throw new McpException($"Prompt '{request.Params.Name}' not found.");
    }
}
