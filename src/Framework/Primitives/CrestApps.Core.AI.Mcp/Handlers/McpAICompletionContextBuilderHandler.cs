using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;

namespace CrestApps.Core.AI.Mcp.Handlers;

internal sealed class McpAICompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    public Task BuildingAsync(AICompletionContextBuildingContext context)
    {
        if (context.Resource is AIProfile profile &&
            profile.TryGet<AIProfileMcpMetadata>(out var mcpMetadata))
        {
            context.Context.McpConnectionIds = mcpMetadata.ConnectionIds;
        }

        return Task.CompletedTask;
    }

    public Task BuiltAsync(AICompletionContextBuiltContext context)
        => Task.CompletedTask;
}
