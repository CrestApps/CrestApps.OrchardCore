using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Mcp.Handlers;

internal sealed class McpAICompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Profile.TryGet<AIProfileMcpMetadata>(out var mcpMetadata))
        {
            context.Context.McpConnectionIds = mcpMetadata.ConnectionIds;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
