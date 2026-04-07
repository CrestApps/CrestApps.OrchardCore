using CrestApps.AI.Models;
using CrestApps.AI.Orchestration;
using CrestApps.AI.Tooling;
using CrestApps.Templates.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.AI.Handlers;

internal sealed class DataSourceOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly AIToolDefinitionOptions _toolDefinitions;
    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;

    public DataSourceOrchestrationHandler(
        IOptions<AIToolDefinitionOptions> toolDefinitions,
        ITemplateService templateService,
        ILogger<DataSourceOrchestrationHandler> logger)
    {
        _toolDefinitions = toolDefinitions.Value;
        _templateService = templateService;
        _logger = logger;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        if (context.OrchestrationContext.CompletionContext == null ||
            string.IsNullOrWhiteSpace(context.OrchestrationContext.CompletionContext.DataSourceId))
        {
            return;
        }

        var dataSourceTools = _toolDefinitions.Tools
            .Where(tool => tool.Value.HasPurpose(AIToolPurposes.DataSourceSearch))
            .Select(tool => tool.Value)
            .ToList();

        if (!context.OrchestrationContext.DisableTools)
        {
            context.OrchestrationContext.MustIncludeTools.AddRange(dataSourceTools.Select(tool => tool.Name));
        }

        var arguments = new Dictionary<string, object>
        {
            ["tools"] = dataSourceTools,
        };

        if (!context.OrchestrationContext.DisableTools)
        {
            arguments["searchToolName"] = SystemToolNames.SearchDataSources;
        }

        var header = await _templateService.RenderAsync(AITemplateIds.DataSourceAvailability, arguments);

        if (!string.IsNullOrEmpty(header))
        {
            context.OrchestrationContext.SystemMessageBuilder.AppendLine();
            context.OrchestrationContext.SystemMessageBuilder.Append(header);
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Enabled data-source orchestration for {ResourceType} and exposed tools: {Tools}.",
                context.Resource.GetType().Name,
                string.Join(", ", dataSourceTools.Select(tool => tool.Name)));
        }
    }
}
