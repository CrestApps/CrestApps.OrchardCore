using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Playwright.Handlers;

internal sealed class PlaywrightOrchestrationContextHandler : IOrchestrationContextBuilderHandler
{
    private readonly IAITemplateService _templateService;
    private readonly ILogger<PlaywrightOrchestrationContextHandler> _logger;

    public PlaywrightOrchestrationContextHandler(
        IAITemplateService templateService,
        ILogger<PlaywrightOrchestrationContextHandler> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    public async Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        if (context.Resource is not Entity entity)
        {
            return;
        }

        var metadata = entity.As<PlaywrightSessionMetadata>();
        if (metadata is null || !metadata.Enabled)
        {
            return;
        }

        context.Context.Properties[nameof(PlaywrightSessionMetadata)] = metadata;

        try
        {
            var prompt = await _templateService.RenderAsync(
                PlaywrightConstants.PromptIds.Operator,
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["baseUrl"] = metadata.BaseUrl ?? string.Empty,
                    ["adminBaseUrl"] = metadata.AdminBaseUrl ?? string.Empty,
                    ["publishBehavior"] = metadata.PublishByDefault
                        ? "Publish by default unless the user explicitly asks for a draft."
                        : "Save a draft by default unless the user explicitly asks to publish.",
                });

            if (!string.IsNullOrWhiteSpace(prompt))
            {
                context.Context.SystemMessageBuilder.AppendLine(prompt.Trim());
                context.Context.SystemMessageBuilder.AppendLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to render the Playwright operator prompt.");
        }
    }

    public Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        if (context.OrchestrationContext.CompletionContext is null ||
            !context.OrchestrationContext.Properties.TryGetValue(nameof(PlaywrightSessionMetadata), out var metadataObject) ||
            metadataObject is not PlaywrightSessionMetadata metadata)
        {
            return Task.CompletedTask;
        }

        context.OrchestrationContext.CompletionContext.AdditionalProperties[PlaywrightConstants.CompletionContextKeys.SessionMetadata] = metadata;

        return Task.CompletedTask;
    }
}
