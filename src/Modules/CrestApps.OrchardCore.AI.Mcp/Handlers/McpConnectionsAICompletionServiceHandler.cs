using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

public sealed class McpConnectionsAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly ISourceCatalog<McpConnection> _store;
    private readonly McpService _mcpService;
    private readonly ILogger _logger;

    public McpConnectionsAICompletionServiceHandler(
        ISourceCatalog<McpConnection> store,
        McpService mcpService,
        ILogger<McpConnectionsAICompletionServiceHandler> logger)
    {
        _store = store;
        _mcpService = mcpService;
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

        context.ChatOptions.Tools ??= [];

        foreach (var mcpConnectionId in context.CompletionContext.McpConnectionIds)
        {
            if (!connections.TryGetValue(mcpConnectionId, out var connection))
            {
                continue;
            }

            try
            {
                var client = await _mcpService.GetOrCreateClientAsync(connection);

                if (client is null)
                {
                    continue;
                }

                foreach (var tool in await client.ListToolsAsync())
                {
                    context.ChatOptions.Tools.Add(tool);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to get the tools from the connection Id '{ConnectionId}' and Name: '{ConnectionName}'", connection.ItemId, connection.DisplayText);
            }
        }
    }
}
