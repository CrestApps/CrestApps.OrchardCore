using System.Text;
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

    public async ValueTask<AICompletionContext> BuildCustomAsync(CustomChatCompletionContext customContext)
    {
        ArgumentNullException.ThrowIfNull(customContext);
        ArgumentNullException.ThrowIfNull(customContext.Session);

        var context = new AICompletionContext();

        var metadata = customContext.Session.As<AIChatInstanceMetadata>();

        if (metadata != null)
        {
            context.SystemMessage = metadata.SystemMessage;
            context.Temperature = metadata.Temperature;
            context.TopP = metadata.TopP;
            context.FrequencyPenalty = metadata.FrequencyPenalty;
            context.PresencePenalty = metadata.PresencePenalty;
            context.MaxTokens = metadata.MaxTokens;
            context.PastMessagesCount = metadata.PastMessagesCount;
            context.UseCaching = metadata.UseCaching;
            context.ToolNames = metadata.ToolNames;
            context.ConnectionName = metadata.ConnectionName;
            context.DeploymentId = metadata.DeploymentId;
        }

        context.InstanceIds = [customContext.CustomChatInstanceId];

        var documents = customContext.Session.Documents?.Items;

        if (documents?.Any() == true)
        {
            var builder = new StringBuilder();

            builder.AppendLine("The user uploaded the following documents.");
            builder.AppendLine("Use this content as authoritative context when answering questions.");
            builder.AppendLine();

            foreach (var document in documents)
            {
                if (string.IsNullOrWhiteSpace(document.TempFilePath))
                {
                    continue;
                }

                if (!File.Exists(document.TempFilePath))
                {
                    continue;
                }

                builder.AppendLine($"--- {document.FileName} ---");

                var text = await File.ReadAllTextAsync(document.TempFilePath);
                builder.AppendLine(text);
                builder.AppendLine();
            }

            context.SystemMessage = string.IsNullOrWhiteSpace(context.SystemMessage) ? builder.ToString() : $"{context.SystemMessage}\n\n{builder}";
        }

        return context;
    }


}
