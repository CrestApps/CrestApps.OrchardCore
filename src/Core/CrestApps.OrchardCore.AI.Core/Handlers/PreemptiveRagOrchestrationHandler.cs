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
            InjectToolSearchInstructions(buildContext, ragMetadata);
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
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                    """

                    [Response Guidelines]
                    Use the provided knowledge source content as the primary basis for your answer.
                    If the provided context does not fully cover the user's question, you may supplement your response with your general knowledge.
                    When citing information from the provided context, include the corresponding reference marker (e.g., [doc:1], [doc:2]) inline immediately after the relevant statement.
                    """);
            }

            return;
        }

        var hasDataSourceRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DataSourceReferences");
        var hasDocumentRefs = buildContext.OrchestrationContext.Properties.ContainsKey("DocumentReferences");

        if (!hasDataSourceRefs && !hasDocumentRefs)
        {
            if (buildContext.OrchestrationContext.DisableTools)
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                    """

                    [Scope Constraint]
                    No relevant content was found in the configured knowledge sources.
                    CRITICAL: You MUST only answer based on the provided knowledge source content.
                    DO NOT use your general knowledge or training data under any circumstances.
                    You MUST inform the user that the answer is not available in the current knowledge sources.
                    """);
            }
            else
            {
                buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                    """

                    [Scope Constraint]
                    No relevant content was found during the initial search of the configured knowledge sources.
                    CRITICAL: You MUST only answer based on knowledge source content. DO NOT use your general knowledge or training data.
                    Before concluding that no answer is available, you MUST call the available search tools (e.g., search_data_source, search_documents) to look for relevant information.
                    If the search tools also return no relevant results, you MUST inform the user that the answer is not available in the current knowledge sources.
                    """);
            }
        }
        else
        {
            buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                """

                [Scope Constraint]
                CRITICAL: You MUST only answer using the knowledge source content provided above.
                DO NOT use your general knowledge or training data under any circumstances.
                If the provided context does not contain information that directly answers the user's question, you MUST respond by telling the user that the requested information is not available in the current knowledge sources. Do not guess, infer, or supplement with outside knowledge.
                When citing information from the provided context, include the corresponding reference marker (e.g., [doc:1], [doc:2]) inline in your response immediately after the relevant statement.
                """);
        }
    }

    /// <summary>
    /// Injects system-message instructions telling the model to use search tools
    /// when preemptive RAG is disabled but data sources or documents are available.
    /// </summary>
    private static void InjectToolSearchInstructions(OrchestrationContextBuiltContext buildContext, AIDataSourceRagMetadata ragMetadata)
    {
        if (ragMetadata?.IsInScope == true)
        {
            // IsInScope ON: the model MUST call search tools and MUST NOT use general knowledge.
            buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                """

                [Knowledge Source Instructions]
                CRITICAL: You have access to internal knowledge sources via search tools (e.g., search_data_source, search_documents).
                You MUST call the relevant search tools to find information BEFORE generating any response.
                DO NOT use your general knowledge or training data under any circumstances.
                If the search tools return no relevant results, you MUST inform the user that the answer is not available in the current knowledge sources. Do not guess, infer, or supplement with outside knowledge.
                When citing information retrieved via tools, include the corresponding reference marker (e.g., [doc:1], [doc:2]) inline in your response immediately after the relevant statement.
                """);
        }
        else
        {
            // IsInScope OFF: the model MUST try search tools first, then may supplement with general knowledge.
            buildContext.OrchestrationContext.SystemMessageBuilder.AppendLine(
                """

                [Knowledge Source Instructions]
                IMPORTANT: You have access to internal knowledge sources via search tools (e.g., search_data_source, search_documents).
                You MUST call the relevant search tools to check for information BEFORE generating any response. Do NOT skip this step.
                After reviewing the search results:
                1. If relevant results are found, use them as the primary basis for your answer and cite using reference markers (e.g., [doc:1], [doc:2]) inline immediately after the relevant statement.
                2. If no relevant results are found, you may then use your general knowledge to answer the question.
                Always search first, then respond.
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
