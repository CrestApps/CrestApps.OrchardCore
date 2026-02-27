using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Orchestration context handler that populates document references
/// from a <see cref="ChatInteraction"/> or <see cref="AIProfile"/> resource
/// and enriches the system message with document metadata so the model knows
/// which documents are available and which tools to use to access them.
/// </summary>
/// <remarks>
/// Document processing tools are registered as system tools and are always included
/// by the orchestrator. This handler provides the model with document metadata
/// and tool descriptions. The resource ID is resolved server-side from
/// <see cref="AIToolExecutionContext.Resource"/> — it is never exposed to the model.
/// </remarks>
public sealed class DocumentOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly IAITemplateService _templateService;

    public DocumentOrchestrationHandler(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        IAITemplateService templateService)
    {
        _toolDefinitions = toolDefinitions.Value;
        _templateService = templateService;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        if (context.Resource is ChatInteraction interaction &&
            interaction.Documents is { Count: > 0 })
        {
            context.Context.Documents = interaction.Documents;
        }
        else if (context.Resource is AIProfile profile)
        {
            var documentsMetadata = profile.As<AIProfileDocumentsMetadata>();

            if (documentsMetadata.Documents is { Count: > 0 })
            {
                context.Context.Documents = documentsMetadata.Documents;
            }
        }

        return Task.CompletedTask;
    }

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        // Check for session documents after configure has run,
        // since the session is set via the configure callback
        // which executes between BuildingAsync and BuiltAsync.
        if (context.OrchestrationContext.Documents is not { Count: > 0 } &&
            context.Resource is AIProfile &&
            context.OrchestrationContext.CompletionContext?.AdditionalProperties is not null &&
            context.OrchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObj) &&
            sessionObj is AIChatSession session &&
            session.Documents is { Count: > 0 })
        {
            context.OrchestrationContext.Documents = session.Documents;
        }

        if (context.OrchestrationContext.Documents is not { Count: > 0 } ||
            context.OrchestrationContext.CompletionContext is null)
        {
            return;
        }

        // Signal document availability so system tools (e.g., search_documents)
        // are included in the tool registry for this completion context.
        context.OrchestrationContext.CompletionContext.AdditionalProperties[AICompletionContextKeys.HasDocuments] = true;

        // Discover document processing tools dynamically by purpose
        // to list their descriptions in the system message.
        var docTools = _toolDefinitions.Tools
            .Where(t => t.Value.HasPurpose(AIToolPurposes.DocumentProcessing))
            .Select(t => t.Value)
            .ToList();

        var arguments = new Dictionary<string, object>
        {
            ["tools"] = docTools,
            ["availableDocuments"] = context.OrchestrationContext.Documents,
        };

        var header = await _templateService.RenderAsync(AITemplateIds.DocumentAvailability, arguments);

        if (!string.IsNullOrEmpty(header))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(header);
        }
    }
}
