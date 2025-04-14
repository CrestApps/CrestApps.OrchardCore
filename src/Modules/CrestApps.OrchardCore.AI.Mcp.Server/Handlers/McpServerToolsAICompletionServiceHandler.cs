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

        var inputStream = new MemoryStream();
        var outputStream = new MemoryStream();

        // TODO, create in-memory transport.
        var transport = new StreamClientTransport(inputStream, outputStream);

        var client = await McpClientFactory.CreateAsync(transport);

        foreach (var tool in await client.ListToolsAsync())
        {
            context.ChatOptions.Tools.Add(tool);
        }
    }
}
