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
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;

    public PreemptiveRagOrchestrationHandler(
        IEnumerable<IPreemptiveRagHandler> handlers,
        PreemptiveSearchQueryProvider queryProvider,
        ISiteService siteService,
        ILogger<PreemptiveRagOrchestrationHandler> logger)
    {
        _handlers = handlers;
        _queryProvider = queryProvider;
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

        // IMPORTANT: If there are no handlers, there is no need to extract queries.
        // This can happen if no feature provides an implementation.
        if (!_handlers.Any())
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<DefaultOrchestratorSettings>();

        // Skip if preemptive RAG is disabled and tools are available.
        // When tools are disabled, preemptive RAG is the only way to inject external context.
        if (!settings.EnablePreemptiveRag && !buildContext.OrchestrationContext.DisableTools)
        {
            return;
        }

        var queries = await _queryProvider.GetQueriesAsync(buildContext.OrchestrationContext);

        if (queries.Count == 0)
        {
            return;
        }

        var ragContext = new PreemptiveRagContext(buildContext.OrchestrationContext, buildContext.Resource, queries);

        await _handlers.InvokeAsync((handler, ctx) => handler.HandleAsync(ctx), ragContext, _logger);

        // After all handlers have run, check if any references were produced.
        // If IsInScope is enabled and no preemptive references exist, inject a scoping
        // directive. The directive is intentionally soft: if the model later discovers
        // relevant content via tool calls (e.g., search_data_source or search_documents),
        // it should use that content instead of refusing to answer.
        var ragMetadata = GetRagMetadata(buildContext.Resource);

        if (ragMetadata?.IsInScope != true)
        {
            return;
        }

        var hasDataSourceRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DataSourceReferences");
        var hasDocumentRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DocumentReferences");

        if (!hasDataSourceRefs && !hasDocumentRefs)
        {
            buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine("\n\n[Scope Constraint]");

            if (buildContext.OrchestrationContext.DisableTools)
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                    """
                    No relevant content was found in the configured knowledge sources.
                    You must only answer based on the provided knowledge source content.
                    Inform the user that the answer is not available in the current knowledge sources.
                    """);
            }
            else
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                    """
                    No relevant content was found during the initial search of the configured knowledge sources.
                    Before concluding that no answer is available, try using the available search tools
                    (e.g., search_data_source, search_documents) to look for relevant information.
                    If the search tools also return no relevant results, then inform the user that the answer is not available in the current knowledge sources.
                    """);
            }
        }
        else
        {
            buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                """

                IMPORTANT: Only answer based on the provided knowledge source content.
                If the provided context does not contain relevant information, inform the user that the answer is not available in the current knowledge sources.
                When citing information from the provided context, include the corresponding reference marker (e.g., [doc:1], [doc:2]) inline in your response immediately after the relevant statement.
                """);

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
