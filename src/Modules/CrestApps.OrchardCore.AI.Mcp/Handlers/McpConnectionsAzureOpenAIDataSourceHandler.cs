using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Tools;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

public sealed class McpConnectionsAzureOpenAIDataSourceHandler : IAzureOpenAIDataSourceHandler
{
    private readonly ISourceCatalog<McpConnection> _store;
    private readonly IMcpServerMetadataCacheProvider _metadataProvider;
    private readonly IMcpMetadataPromptGenerator _promptGenerator;
    private readonly McpInvokeFunction _mcpInvokeFunction;
    private readonly ILogger _logger;

    public McpConnectionsAzureOpenAIDataSourceHandler(
        ISourceCatalog<McpConnection> store,
        IMcpServerMetadataCacheProvider metadataProvider,
        IMcpMetadataPromptGenerator promptGenerator,
        McpInvokeFunction mcpInvokeFunction,
        ILogger<McpConnectionsAzureOpenAIDataSourceHandler> logger)
    {
        _store = store;
        _metadataProvider = metadataProvider;
        _promptGenerator = promptGenerator;
        _mcpInvokeFunction = mcpInvokeFunction;
        _logger = logger;
    }

    public ValueTask ConfigureSourceAsync(ChatCompletionOptions options, AzureOpenAIDataSourceContext context)
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask ConfigureOptionsAsync(AzureOpenAIChatOptionsContext context)
    {
        if (context.CompletionContext.DisableTools ||
            context.CompletionContext.McpConnectionIds is null ||
            context.CompletionContext.McpConnectionIds.Length == 0)
        {
            return;
        }

        var connections = (await _store.GetAllAsync())
            .ToDictionary(x => x.ItemId);

        if (connections.Count == 0)
        {
            return;
        }

        var allCapabilities = new List<McpServerCapabilities>();

        foreach (var mcpConnectionId in context.CompletionContext.McpConnectionIds)
        {
            if (!connections.TryGetValue(mcpConnectionId, out var connection))
            {
                continue;
            }

            try
            {
                var capabilities = await _metadataProvider.GetCapabilitiesAsync(connection);

                if (capabilities is not null)
                {
                    allCapabilities.Add(capabilities);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to get capabilities from MCP connection Id '{ConnectionId}' and Name: '{ConnectionName}'.", connection.ItemId, connection.DisplayText);
            }
        }

        if (allCapabilities.Count == 0)
        {
            return;
        }

        // Add the unified mcp-invoke tool as a system function so it is
        // both registered in ChatCompletionOptions.Tools and available for invocation.
        context.SystemFunctions.Add(_mcpInvokeFunction);

        // Inject the metadata system prompt describing available capabilities.
        var metadataPrompt = _promptGenerator.Generate(allCapabilities);

        if (!string.IsNullOrEmpty(metadataPrompt))
        {
            context.Prompts.Insert(0, new SystemChatMessage(metadataPrompt));
        }
    }
}
