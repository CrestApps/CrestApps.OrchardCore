using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Server.Handlers;

public sealed class McpServerToolsAICompletionServiceHandler : IAICompletionServiceHandler
{
    public async Task ConfigureAsync(CompletionServiceConfigureContext context)
    {
        if (!context.IsFunctionInvocationSupported)
        {
            return;
        }

        var mcpMetadata = context.Profile.As<McpServerMetadata>();

        if (mcpMetadata.UseLocalServer)
        {
            return;
        }

        context.ChatOptions.Tools ??= [];

        // TODO, create in-memory transport.
        var transport = new SseClientTransport(new SseClientTransportOptions()
        {
            Name = "test",
            Endpoint = new Uri("https://localhost/test"),
        });

        var client = await McpClientFactory.CreateAsync(transport);

        foreach (var tool in await client.ListToolsAsync())
        {
            context.ChatOptions.Tools.Add(tool);
        }
    }
}
