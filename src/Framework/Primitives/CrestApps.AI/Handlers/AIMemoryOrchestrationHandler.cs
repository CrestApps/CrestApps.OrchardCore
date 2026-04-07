using CrestApps.AI.Completions;
using CrestApps.AI.Memory;
using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.Tooling;
using CrestApps.AI.Tools;
using CrestApps.Templates.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Handlers;

internal sealed class AIMemoryOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly ITemplateService _templateService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<ChatInteractionMemoryOptions> _chatInteractionMemoryOptions;
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly ILogger _logger;

    public AIMemoryOrchestrationHandler(
        ITemplateService templateService,
        IHttpContextAccessor httpContextAccessor,
        IOptions<ChatInteractionMemoryOptions> chatInteractionMemoryOptions,
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        ILogger<AIMemoryOrchestrationHandler> logger)
    {
        _templateService = templateService;
        _httpContextAccessor = httpContextAccessor;
        _chatInteractionMemoryOptions = chatInteractionMemoryOptions;
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

        var isEnabled = AIMemoryOrchestrationContextHelper.IsEnabled(context.Resource, _chatInteractionMemoryOptions);

        if (!isEnabled)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping memory orchestration for {ResourceType}: memory is disabled.", context.Resource.GetType().Name);
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
