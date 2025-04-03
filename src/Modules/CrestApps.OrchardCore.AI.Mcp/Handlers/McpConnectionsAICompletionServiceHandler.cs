using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using ModelContextProtocol.Client;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AzureAIInference.Handlers;

public sealed class McpConnectionsAICompletionServiceHandler : IAICompletionServiceHandler
{
    private readonly IModelStore<McpConnection> _store;

    public McpConnectionsAICompletionServiceHandler(IModelStore<McpConnection> store)
    {
        _store = store;
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

        var connections = (await _store.GetAllAsync()).ToDictionary(x => x.Id);

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

            var client = await McpClientFactoryHelpers.CreateAsync(connection);

            foreach (var tool in await client.ListToolsAsync())
            {
                context.ChatOptions.Tools.Add(tool);
            }
        }
    }
}
