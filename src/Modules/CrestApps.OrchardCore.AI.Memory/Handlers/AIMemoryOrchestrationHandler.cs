using System.Security.Claims;
using CrestApps.AI.Prompting.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Memory.Models;
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

        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Skipping memory orchestration for {ResourceType}: user is not authenticated.", context.Resource.GetType().Name);
            }

            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        var isEnabled = !string.IsNullOrEmpty(userId) && await IsEnabledAsync(context);

        if (string.IsNullOrEmpty(userId) || !isEnabled)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Skipping memory orchestration for {ResourceType}: userId present={HasUserId}, enabled={IsEnabled}.",
                    context.Resource.GetType().Name,
                    !string.IsNullOrEmpty(userId),
                    isEnabled);
            }

            return;
        }

        context.OrchestrationContext.CompletionContext.AdditionalProperties[MemoryConstants.CompletionContextKeys.HasMemory] = true;
        AIInvocationScope.Current?.Items.TryAdd(MemoryConstants.CompletionContextKeys.UserId, userId);

        var memoryTools = _toolDefinitions.Tools
            .Where(t => t.Value.HasPurpose(AIToolPurposes.Memory))
            .Select(t => t.Value)
            .ToList();

        var header = await _templateService.RenderAsync(
            MemoryConstants.TemplateIds.MemoryAvailability,
            new Dictionary<string, object>
            {
                ["tools"] = memoryTools,
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

    private async Task<bool> IsEnabledAsync(OrchestrationContextBuiltContext context)
    {
        if (context.Resource is AIProfile profile)
        {
            return profile.GetSettings<AIProfileMemorySettings>().EnableUserMemory;
        }

        if (context.Resource is ChatInteraction)
        {
            return (await _siteService.GetSettingsAsync<ChatInteractionMemorySettings>()).EnableUserMemory;
        }

        return false;
    }
}
