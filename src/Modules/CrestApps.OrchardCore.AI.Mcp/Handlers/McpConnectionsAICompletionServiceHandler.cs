using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Tools;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

public sealed class McpConnectionsAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly ISourceCatalog<McpConnection> _store;
    private readonly IMcpServerMetadataCacheProvider _metadataProvider;
    private readonly McpInvokeFunction _mcpInvokeFunction;
    private readonly IMcpMetadataPromptGenerator _promptGenerator;
    private readonly ILogger _logger;

    public McpConnectionsAICompletionServiceHandler(
        ISourceCatalog<McpConnection> store,
        IMcpServerMetadataCacheProvider metadataProvider,
        McpInvokeFunction mcpInvokeFunction,
        IMcpMetadataPromptGenerator promptGenerator,
        ILogger<McpConnectionsAICompletionServiceHandler> logger)
    {
        _store = store;
        _metadataProvider = metadataProvider;
        _mcpInvokeFunction = mcpInvokeFunction;
        _promptGenerator = promptGenerator;
        _logger = logger;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (!context.IsFunctionInvocationSupported ||
            context.CompletionContext is null ||
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

        // Inject the unified mcp-invoke tool.
        context.ChatOptions.Tools ??= [];
        context.ChatOptions.Tools.Add(_mcpInvokeFunction);

        // Inject the metadata system prompt describing available capabilities.
        var metadataPrompt = _promptGenerator.Generate(allCapabilities);

        if (!string.IsNullOrEmpty(metadataPrompt))
        {
            context.CompletionContext.SystemMessage = string.IsNullOrEmpty(context.CompletionContext.SystemMessage)
                ? metadataPrompt
                : context.CompletionContext.SystemMessage + Environment.NewLine + metadataPrompt;
        }
    }
}
