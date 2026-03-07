using CrestApps.AI.Models;
using CrestApps.AI.Prompting.Services;
using CrestApps.AI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Handlers;

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
    private readonly DefaultOrchestratorSettings _settings;
    private readonly ILogger _logger;

    public PreemptiveRagOrchestrationHandler(
        IEnumerable<IPreemptiveRagHandler> handlers,
        PreemptiveSearchQueryProvider queryProvider,
        IAITemplateService templateService,
        IOptions<DefaultOrchestratorSettings> settings,
        ILogger<PreemptiveRagOrchestrationHandler> logger)
    {
        _handlers = handlers;
        _queryProvider = queryProvider;
        _templateService = templateService;
        _settings = settings.Value;
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

        if (usableHandlers.Count == 0)
        {
            return;
        }

        var ragMetadata = GetRagMetadata(buildContext.Resource);

        // When preemptive RAG is disabled and tools are available, we skip the upfront
        // search but still inject instructions so the model knows to call search tools.
        if (!_settings.EnablePreemptiveRag && !buildContext.OrchestrationContext.DisableTools)
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

        foreach (var handler in usableHandlers)
        {
            try
            {
                await handler.HandleAsync(ragContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Preemptive RAG handler '{HandlerType}' failed.", handler.GetType().Name);
            }
        }

        // After all handlers have run, check if any references were produced.
        if (ragMetadata?.IsInScope != true)
        {
            var hasAnyRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DataSourceReferences")
                || buildContext.OrchestrationContext.Properties.ContainsKey("DocumentReferences");

            if (hasAnyRefs)
            {
                var prompt = await _templateService.RenderAsync(AITemplateIds.RagResponseGuidelines);

                if (!string.IsNullOrEmpty(prompt))
                {
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine();
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
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine();
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
                }
            }
            else
            {
                var prompt = await _templateService.RenderAsync(AITemplateIds.RagScopeNoRefsToolsEnabled);

                if (!string.IsNullOrEmpty(prompt))
                {
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine();
                    buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
                }
            }
        }
        else
        {
            var prompt = await _templateService.RenderAsync(AITemplateIds.RagScopeWithRefs);

            if (!string.IsNullOrEmpty(prompt))
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine();
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
            }
        }
    }

    private async Task InjectToolSearchInstructionsAsync(OrchestrationContextBuiltContext buildContext, AIDataSourceRagMetadata ragMetadata)
    {
        if (ragMetadata?.IsInScope == true)
        {
            var prompt = await _templateService.RenderAsync(AITemplateIds.RagToolSearchStrict);

            if (!string.IsNullOrEmpty(prompt))
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine();
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(prompt);
            }
        }
        else
        {
            var prompt = await _templateService.RenderAsync(AITemplateIds.RagToolSearchRelaxed);

            if (!string.IsNullOrEmpty(prompt))
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine();
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
