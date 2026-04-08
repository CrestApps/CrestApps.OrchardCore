using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Handlers;

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
    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;

    public DocumentOrchestrationHandler(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        ITemplateService templateService,
        ILogger<DocumentOrchestrationHandler> logger)
    {
        _toolDefinitions = toolDefinitions.Value;
        _templateService = templateService;
        _logger = logger;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
    {
        if (context.Resource is ChatInteraction interaction &&
            interaction.Documents is { Count: > 0 })
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Populating {DocCount} document(s) from ChatInteraction '{ItemId}' into orchestration context.",
                interaction.Documents.Count, interaction.ItemId);
            }

            context.Context.Documents ??= [];
            context.Context.Documents.AddRange(interaction.Documents);
        }
        else if (context.Resource is AIProfile profile)
        {
            var documentsMetadata = profile.As<DocumentsMetadata>();

            if (documentsMetadata.Documents is { Count: > 0 })
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Populating {DocCount} document(s) from AIProfile '{ProfileId}' into orchestration context.",
                    documentsMetadata.Documents.Count, profile.ItemId);
                }

                context.Context.Documents ??= [];
                context.Context.Documents.AddRange(documentsMetadata.Documents);
            }
            else if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("AIProfile '{ProfileId}' has no documents attached.", profile.ItemId);
            }
        }

        return Task.CompletedTask;
    }

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        IEnumerable<ChatDocumentInfo> knowledgeBaseDocuments = null;
        IEnumerable<ChatDocumentInfo> userSuppliedDocuments = null;

        if (context.Resource is ChatInteraction interaction && interaction.Documents is { Count: > 0 })
        {
            userSuppliedDocuments = interaction.Documents;
        }
        else if (context.Resource is AIProfile profile)
        {
            var documentsMetadata = profile.As<DocumentsMetadata>();
            knowledgeBaseDocuments = documentsMetadata.Documents;

            if (context.OrchestrationContext.CompletionContext?.AdditionalProperties is not null &&
                context.OrchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObj) &&
                    sessionObj is AIChatSession session &&
                        session.Documents is { Count: > 0 })
            {
                userSuppliedDocuments = session.Documents;
            }
        }

        var ragMetadata = GetRagMetadata(context.Resource);

        var hasKnowledgeBaseDocuments = knowledgeBaseDocuments?.Any() == true;
        var hasUserSuppliedDocuments = userSuppliedDocuments?.Any() == true;

        if (!hasKnowledgeBaseDocuments && !hasUserSuppliedDocuments &&
            context.OrchestrationContext.Documents is { Count: > 0 } existingDocuments)
        {
            var clonedDocuments = existingDocuments.ToArray();

            if (context.Resource is AIProfile)
            {
                knowledgeBaseDocuments = clonedDocuments;
                hasKnowledgeBaseDocuments = true;
            }
            else
            {
                userSuppliedDocuments = clonedDocuments;
                hasUserSuppliedDocuments = true;
            }
        }

        if ((!hasKnowledgeBaseDocuments && !hasUserSuppliedDocuments) || context.OrchestrationContext.CompletionContext is null)
        {
            return;
        }

        context.OrchestrationContext.Documents ??= [];
        context.OrchestrationContext.Documents.Clear();

        if (hasKnowledgeBaseDocuments)
        {
            context.OrchestrationContext.Documents.AddRange(knowledgeBaseDocuments);
        }

        if (hasUserSuppliedDocuments)
        {
            context.OrchestrationContext.Documents.AddRange(userSuppliedDocuments);
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
            ["knowledgeBaseDocuments"] = hasKnowledgeBaseDocuments ? knowledgeBaseDocuments : Array.Empty<ChatDocumentInfo>(),
            ["userSuppliedDocuments"] = hasUserSuppliedDocuments ? userSuppliedDocuments : Array.Empty<ChatDocumentInfo>(),
            ["isInScope"] = ragMetadata?.IsInScope == true,
        };

        var header = await _templateService.RenderAsync(AITemplateIds.DocumentAvailability, arguments);

        if (!string.IsNullOrEmpty(header))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(header);
        }
    }

    private static AIDataSourceRagMetadata GetRagMetadata(object resource)
    {
        if (resource is AIProfile profile &&
            profile.TryGet<AIDataSourceRagMetadata>(out var profileMetadata))
        {
            return profileMetadata;
        }

        if (resource is ChatInteraction interaction &&
            interaction.TryGet<AIDataSourceRagMetadata>(out var interactionMetadata))
        {
            return interactionMetadata;
        }

        return null;
    }
}
