using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Memory.Tools;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

internal sealed class AIMemoryOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly IAITemplateService _templateService;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly ILogger _logger;

    public AIMemoryOrchestrationHandler(
        IAITemplateService templateService,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        ILogger<AIMemoryOrchestrationHandler> logger)
    {
        _templateService = templateService;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _toolDefinitions = toolDefinitions.Value;
        _logger = logger;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        if (context.OrchestrationContext.CompletionContext is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping memory orchestration for {ResourceType}: completion context is null.", context.Resource.GetType().Name);
            }

            return;
        }

        var userId = AIMemoryOrchestrationContextHelper.GetAuthenticatedUserId(_httpContextAccessor);

        if (string.IsNullOrEmpty(userId))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping memory orchestration for {ResourceType}: user is not authenticated.", context.Resource.GetType().Name);
            }

            return;
        }

        var isEnabled = await AIMemoryOrchestrationContextHelper.IsEnabledAsync(context.Resource, _siteService);

        if (!isEnabled)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping memory orchestration for {ResourceType}: enabled={IsEnabled}.",
                    context.Resource.GetType().Name,
                    isEnabled);
            }

            return;
        }

        context.OrchestrationContext.CompletionContext.AdditionalProperties[AICompletionContextKeys.HasMemory] = true;
        AIInvocationScope.Current?.Items.TryAdd(MemoryConstants.CompletionContextKeys.UserId, userId);

        var memoryTools = _toolDefinitions.Tools
            .Where(t => t.Value.HasPurpose(AIToolPurposes.Memory))
            .Select(t => t.Value)
            .ToList();

        context.OrchestrationContext.MustIncludeTools.AddRange(memoryTools.Select(tool => tool.Name));

        var header = await _templateService.RenderAsync(
            MemoryConstants.TemplateIds.MemoryAvailability,
            new Dictionary<string, object>
            {
                ["tools"] = memoryTools,
                ["searchToolName"] = SearchUserMemoriesTool.TheName,
                ["listToolName"] = ListUserMemoriesTool.TheName,
                ["saveToolName"] = SaveUserMemoryTool.TheName,
                ["removeToolName"] = RemoveUserMemoryTool.TheName,
            });

        if (!string.IsNullOrEmpty(header))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(header);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Enabled memory orchestration for {ResourceType} and exposed tools: {Tools}.",
                context.Resource.GetType().Name,
                string.Join(", ", memoryTools.Select(tool => tool.Name)));
        }
    }
}
