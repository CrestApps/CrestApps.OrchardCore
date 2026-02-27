using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Orchestration handler that coordinates preemptive RAG across all registered
/// <see cref="IPreemptiveRagHandler"/> implementations. Extracts focused search queries
/// once using <see cref="PreemptiveSearchQueryProvider"/> and dispatches them to each handler.
/// After all handlers have run, evaluates the IsInScope constraint and injects a scoping
/// directive if no references were produced.
/// </summary>
internal sealed class PreemptiveRagOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly IEnumerable<IPreemptiveRagHandler> _handlers;
    private readonly PreemptiveSearchQueryProvider _queryProvider;
    private readonly IAITemplateService _templateService;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    public PreemptiveRagOrchestrationHandler(
        IEnumerable<IPreemptiveRagHandler> handlers,
        PreemptiveSearchQueryProvider queryProvider,
        IAITemplateService templateService,
        ISiteService siteService,
        ILogger<PreemptiveRagOrchestrationHandler> logger)
    {
        _handlers = handlers;
        _queryProvider = queryProvider;
        _templateService = templateService;
        _siteService = siteService;
        _logger = logger;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public async Task BuiltAsync(OrchestrationContextBuiltContext buildContext)
    {
        if (string.IsNullOrEmpty(buildContext.OrchestrationContext.UserMessage))
        {
            return;
        }

        // IMPORTANT: If there are no handlers, there is no need to keep going or check settings.
        // This can happen if no feature provides an implementation.
        if (!_handlers.Any())
        {
            return;
        }

        var usableHandlers = new List<IPreemptiveRagHandler>();

        foreach (var handler in _handlers)
        {
            if (!await handler.CanHandleAsync(buildContext))
            {
                continue;
            }

            usableHandlers.Add(handler);
        }

        // IMPORTANT: If there are no usable-handlers, there is no need to extract queries.
        // This can happen if no feature provides an implementation.
        if (usableHandlers.Count == 0)
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<DefaultOrchestratorSettings>();
        var ragMetadata = GetRagMetadata(buildContext.Resource);

        // When preemptive RAG is disabled and tools are available, we skip the upfront
        // search but still inject instructions so the model knows to call search tools.
        if (!settings.EnablePreemptiveRag && !buildContext.OrchestrationContext.DisableTools)
        {
            await InjectToolSearchInstructionsAsync(buildContext, ragMetadata);
            return;
        }

        var queries = await _queryProvider.GetQueriesAsync(buildContext.OrchestrationContext);

        if (queries.Count == 0)
        {
            return;
        }

        var ragContext = new PreemptiveRagContext(buildContext.OrchestrationContext, buildContext.Resource, queries);

        await usableHandlers.InvokeAsync((handler, ctx) => handler.HandleAsync(ctx), ragContext, _logger);

        // After all handlers have run, check if any references were produced.
        // If IsInScope is enabled and no preemptive references exist, inject a scoping
        // directive. The directive is intentionally soft: if the model later discovers
        // relevant content via tool calls (e.g., search_data_source or search_documents),
        // it should use that content instead of refusing to answer.
        if (ragMetadata?.IsInScope != true)
        {
            // When IsInScope is false but references exist, explicitly tell the model it CAN
            // supplement with general knowledge. Without this, some models treat injected context
            // as the only allowed source even without a scope constraint.
            var hasAnyRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DataSourceReferences")
                || buildContext.OrchestrationContext.Properties.ContainsKey("DocumentReferences");

            if (hasAnyRefs)
            {
                var prompt = await _templateService.RenderAsync(AITemplateIds.RagResponseGuidelines);

                if (!string.IsNullOrEmpty(prompt))
                {
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
                }
            }

            return;
        }

        var hasDataSourceRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DataSourceReferences");
        var hasDocumentRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DocumentReferences");

        if (!hasDataSourceRefs && !hasDocumentRefs)
        {
            if (buildContext.OrchestrationContext.DisableTools)
            {
                var prompt = await _templateService.RenderAsync(AITemplateIds.RagScopeNoRefsToolsDisabled);

                if (!string.IsNullOrEmpty(prompt))
                {
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
                }
            }
            else
            {
                var prompt = await _templateService.RenderAsync(AITemplateIds.RagScopeNoRefsToolsEnabled);

                if (!string.IsNullOrEmpty(prompt))
                {
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
                }
            }
        }
        else
        {
            var prompt = await _templateService.RenderAsync(AITemplateIds.RagScopeWithRefs);

            if (!string.IsNullOrEmpty(prompt))
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
            }
        }
    }

    /// <summary>
    /// Injects system-message instructions telling the model to use search tools
    /// when preemptive RAG is disabled but data sources or documents are available.
    /// </summary>
    private async Task InjectToolSearchInstructionsAsync(OrchestrationContextBuiltContext buildContext, AIDataSourceRagMetadata ragMetadata)
    {
        if (ragMetadata?.IsInScope == true)
        {
            // IsInScope ON: the model MUST call search tools and MUST NOT use general knowledge.
            var prompt = await _templateService.RenderAsync(AITemplateIds.RagToolSearchStrict);

            if (!string.IsNullOrEmpty(prompt))
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
            }
        }
        else
        {
            // IsInScope OFF: the model MUST try search tools first, then may supplement with general knowledge.
            var prompt = await _templateService.RenderAsync(AITemplateIds.RagToolSearchRelaxed);

            if (!string.IsNullOrEmpty(prompt))
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
            }
        }
    }

    private static AIDataSourceRagMetadata GetRagMetadata(object resource)
    {
        if (resource is AIProfile profile &&
            profile.TryGet<AIDataSourceRagMetadata>(out var ragMetadata))
        {
            return ragMetadata;
        }

        if (resource is ChatInteraction interaction &&
            interaction.TryGet<AIDataSourceRagMetadata>(out var interactionRagMetadata))
        {
            return interactionRagMetadata;
        }

        return null;
    }
}
