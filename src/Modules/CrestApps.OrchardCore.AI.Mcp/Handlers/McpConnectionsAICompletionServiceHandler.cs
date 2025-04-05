using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AzureAIInference.Handlers;

public sealed class McpConnectionsAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly IModelStore<McpConnection> _store;
    private readonly McpService _mcpService;

    public McpConnectionsAICompletionServiceHandler(
        IModelStore<McpConnection> store,
        McpService mcpService)
    {
        _store = store;
        _mcpService = mcpService;
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

            foreach (var tool in await _mcpService.GetToolsAsync(connection))
            {
                context.ChatOptions.Tools.Add(tool);
            }
        }
    }
}
