using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Tools;
using CrestApps.Core.Templates.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.Core.AI.Handlers;

internal sealed class AIMemoryPreemptiveRagHandler : IPreemptiveRagHandler
{
    private readonly IAIMemorySearchService _memorySearchService;
    private readonly ITemplateService _templateService;
    private readonly GeneralAIOptions _generalAIOptions;
    private readonly IOptions<ChatInteractionMemoryOptions> _chatInteractionMemoryOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    public AIMemoryPreemptiveRagHandler(
        IAIMemorySearchService memorySearchService,
        ITemplateService templateService,
        IOptions<GeneralAIOptions> generalAIOptions,
        IOptions<ChatInteractionMemoryOptions> chatInteractionMemoryOptions,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AIMemoryPreemptiveRagHandler> logger)
    {
        _memorySearchService = memorySearchService;
        _templateService = templateService;
        _generalAIOptions = generalAIOptions.Value;
        _chatInteractionMemoryOptions = chatInteractionMemoryOptions;
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

        if (!_generalAIOptions.EnablePreemptiveMemoryRetrieval)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("AI memory preemptive RAG skipped for {ResourceType}: preemptive memory retrieval is disabled.", context.Resource.GetType().Name);
            }

            return false;
        }

        var isEnabled = AIMemoryOrchestrationContextHelper.IsEnabled(context.Resource, _chatInteractionMemoryOptions);

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
