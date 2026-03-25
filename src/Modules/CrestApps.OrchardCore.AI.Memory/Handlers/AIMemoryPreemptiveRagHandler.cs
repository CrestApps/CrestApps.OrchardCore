using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Memory.Services;
using CrestApps.OrchardCore.AI.Memory.Tools;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

internal sealed class AIMemoryPreemptiveRagHandler : IPreemptiveRagHandler
{
    private readonly AIMemorySearchService _memorySearchService;
    private readonly IAITemplateService _templateService;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    public AIMemoryPreemptiveRagHandler(
        AIMemorySearchService memorySearchService,
        IAITemplateService templateService,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AIMemoryPreemptiveRagHandler> logger)
    {
        _memorySearchService = memorySearchService;
        _templateService = templateService;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async ValueTask<bool> CanHandleAsync(OrchestrationContextBuiltContext context)
    {
        var userId = AIMemoryOrchestrationContextHelper.GetAuthenticatedUserId(_httpContextAccessor);

        if (string.IsNullOrEmpty(userId))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("AI memory preemptive RAG skipped for {ResourceType}: user is not authenticated.", context.Resource.GetType().Name);
            }

            return false;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var generalSettings = site.As<GeneralAISettings>();

        if (generalSettings is not null && !generalSettings.EnablePreemptiveMemoryRetrieval)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("AI memory preemptive RAG skipped for {ResourceType}: preemptive memory retrieval is disabled.", context.Resource.GetType().Name);
            }

            return false;
        }

        var isEnabled = await AIMemoryOrchestrationContextHelper.IsEnabledAsync(context.Resource, _siteService);

        if (!isEnabled && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("AI memory preemptive RAG skipped for {ResourceType}: memory is disabled.", context.Resource.GetType().Name);
        }

        return isEnabled;
    }

    public async Task HandleAsync(PreemptiveRagContext context)
    {
        var userId = AIMemoryOrchestrationContextHelper.GetAuthenticatedUserId(_httpContextAccessor);

        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        var results = (await _memorySearchService.SearchAsync(userId, context.Queries, requestedTopN: null))
            .Where(result => !string.IsNullOrWhiteSpace(result.Content))
            .ToList();

        if (results.Count == 0)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("AI memory preemptive RAG found no matching memories for {QueryCount} query candidate(s).", context.Queries.Count);
            }

            return;
        }

        var arguments = new Dictionary<string, object>
        {
            ["results"] = results.Select(result => new
            {
                result.Name,
                result.Description,
                result.Content,
                UpdatedUtc = result.UpdatedUtc?.ToString("O"),
            }).ToList(),
        };

        if (!context.OrchestrationContext.DisableTools)
        {
            arguments["searchToolName"] = SearchUserMemoriesTool.TheName;
        }

        var header = await _templateService.RenderAsync(MemoryConstants.TemplateIds.MemoryContextHeader, arguments);

        if (!string.IsNullOrEmpty(header))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(header);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("AI memory preemptive RAG injected {ResultCount} memory entries into the system message.", results.Count);
        }
    }
}
