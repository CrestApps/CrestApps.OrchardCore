using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Extensions;

public static class AIProfileExtensions
{
    public static AICompletionContext AsAICompletionContext(this AIProfile profile, Action<AICompletionContext> callback = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var context = new AICompletionContext()
        {
            ConnectionName = profile.ConnectionName,
        };

        if (profile.TryGet<AIProfileMetadata>(out var metadata))
        {
            context.SystemMessage = metadata.SystemMessage;
            context.Temperature = metadata.Temperature;
            context.TopP = metadata.TopP;
            context.FrequencyPenalty = metadata.FrequencyPenalty;
            context.PresencePenalty = metadata.PresencePenalty;
            context.MaxTokens = metadata.MaxTokens;
            context.PastMessagesCount = metadata.PastMessagesCount;
            context.UseCaching = metadata.UseCaching;
        }

        if (profile.TryGet<AIProfileFunctionInvocationMetadata>(out var functionInvocationMetadata))
        {
            context.ToolNames = functionInvocationMetadata.Names;
        }

        if (profile.TryGet<AIProfileFunctionInstancesMetadata>(out var functionInstancesMetadata))
        {
            context.InstanceIds = functionInstancesMetadata.InstanceIds;
        }

        if (profile.TryGet<AIProfileMcpMetadata>(out var mcpMetadata))
        {
            context.McpConnectionIds = mcpMetadata.ConnectionIds;
        }

        if (profile.TryGet<AIProfileDataSourceMetadata>(out var dataSourceMetadata))
        {
            context.DataSourceType = dataSourceMetadata.DataSourceType;
            context.DataSourceId = dataSourceMetadata.DataSourceId;
        }

        // Invoke the callback to allow further customization after the initial mapping to allow the user to override any values.
        if (callback is not null)
        {
            callback(context);
        }

        return context;

    }
}
