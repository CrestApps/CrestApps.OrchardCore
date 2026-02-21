using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Orchestration handler that coordinates preemptive RAG across all registered
/// <see cref="IPreemptiveRagHandler"/> implementations. Extracts focused search queries
/// once using <see cref="PreemptiveSearchQueryProvider"/> and dispatches them to each handler.
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

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        if (string.IsNullOrEmpty(context.Context.UserMessage))
        {
            return;
        }

        // IMPORTANRT: If there are no handler, there is no need to extract queries.
        // This can happen if no feature provide implemenation.
        if (!_handlers.Any())
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<DefaultOrchestratorSettings>();

        // Skip if preemptive RAG is disabled and tools are available.
        // When tools are disabled, preemptive RAG is the only way to inject external context.
        if (!settings.EnablePreemptiveRag && !context.Context.DisableTools)
        {
            return;
        }

        var queries = await _queryProvider.GetQueriesAsync(context.Context);

        if (queries.Count == 0)
        {
            return;
        }

        var ragContext = new PreemptiveRagContext(context.Context, context.Resource, queries);

        await _handlers.InvokeAsync((handler, context) => handler.HandleAsync(context), ragContext, _logger);
    }
}
