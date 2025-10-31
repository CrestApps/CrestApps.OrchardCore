using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Services;

public sealed class DefaultAICompletionContextBuilder : IAICompletionContextBuilder
{
    private readonly IEnumerable<IAICompletionContextBuilderHandler> _handlers;
    private readonly ILogger _logger;

    public DefaultAICompletionContextBuilder(
        IEnumerable<IAICompletionContextBuilderHandler> handlers,
        ILogger<DefaultAICompletionContextBuilder> logger)
    {
        _handlers = handlers?.Reverse() ?? [];
        _logger = logger;
    }

    public async ValueTask<AICompletionContext> BuildAsync(AIProfile profile, Action<AICompletionContext> configure = null)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var context = new AICompletionContext()
        {
            ConnectionName = profile.ConnectionName,
            DeploymentId = profile.DeploymentId,
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

        var building = new AICompletionContextBuildingContext(profile, context);
        await _handlers.InvokeAsync((h, c) => h.BuildingAsync(c), building, _logger);

        // Allow caller override last.
        configure?.Invoke(context);

        var built = new AICompletionContextBuiltContext(profile, context);
        await _handlers.InvokeAsync((h, c) => h.BuiltAsync(c), built, _logger);

        return context;
    }
}
