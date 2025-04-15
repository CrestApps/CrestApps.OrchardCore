using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

public sealed class McpConnectionsAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly ISourceModelStore<McpConnection> _store;
    private readonly McpService _mcpService;
    private readonly ILogger _logger;

    public McpConnectionsAICompletionServiceHandler(
        ISourceModelStore<McpConnection> store,
        McpService mcpService,
        ILogger<McpConnectionsAICompletionServiceHandler> logger)
    {
        _store = store;
        _mcpService = mcpService;
        _logger = logger;
    }

    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (!context.IsFunctionInvocationSupported)
        {
            return;
        }

        var mcpMetadata = context.Profile.As<AIProfileMcpMetadata>();

        if (mcpMetadata.ConnectionIds is null || mcpMetadata.ConnectionIds.Length == 0)
        {
            return;
        }

        var connections = (await _store.GetAllAsync())
            .ToDictionary(x => x.Id);

        if (connections.Count == 0)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        foreach (var connectionId in mcpMetadata.ConnectionIds)
        {
            if (!connections.TryGetValue(connectionId, out var connection))
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
                _logger.LogError(ex, "Unable to get the tools from the connection Id '{ConnectionId}' and Name: '{ConnectionName}'", connection.Id, connection.DisplayText);
            }
        }
    }
}
