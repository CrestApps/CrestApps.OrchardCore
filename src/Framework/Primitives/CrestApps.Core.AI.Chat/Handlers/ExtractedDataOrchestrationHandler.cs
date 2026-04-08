using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.Templates.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Chat.Handlers;

/// <summary>
/// Injects already extracted chat-session fields into the live orchestration context
/// so scripted profiles do not keep asking for values that are already known.
/// </summary>
public sealed class ExtractedDataOrchestrationHandler : IOrchestrationContextBuilderHandler
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<ExtractedDataOrchestrationHandler> _logger;

    public ExtractedDataOrchestrationHandler(
        ITemplateService templateService,
        ILogger<ExtractedDataOrchestrationHandler> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    public Task BuildingAsync(OrchestrationContextBuildingContext context)
        => Task.CompletedTask;

    public async Task BuiltAsync(OrchestrationContextBuiltContext context)
    {
        if (context.Resource is not AIProfile profile ||
            context.OrchestrationContext.CompletionContext is null ||
            !profile.TryGetSettings<AIProfileDataExtractionSettings>(out var settings) ||
            !settings.EnableDataExtraction ||
            settings.DataExtractionEntries.Count == 0 ||
            !context.OrchestrationContext.CompletionContext.AdditionalProperties.TryGetValue("Session", out var sessionObject) ||
            sessionObject is not AIChatSession session ||
            session.ExtractedData.Count == 0)
        {
            return;
        }

        var collectedFields = settings.DataExtractionEntries
            .Where(entry =>
                session.ExtractedData.TryGetValue(entry.Name, out var state) &&
                state?.Values.Count > 0)
            .Select(entry => new
            {
                entry.Name,
                entry.Description,
                Values = session.ExtractedData[entry.Name].Values,
                entry.AllowMultipleValues,
                entry.IsUpdatable,
            })
            .ToList();

        if (collectedFields.Count == 0)
        {
            return;
        }

        var missingFields = settings.DataExtractionEntries
            .Where(entry =>
                !session.ExtractedData.TryGetValue(entry.Name, out var state) ||
                state?.Values.Count == 0)
            .Select(entry => new
            {
                entry.Name,
                entry.Description,
                entry.AllowMultipleValues,
                entry.IsUpdatable,
            })
            .ToList();

        var header = await _templateService.RenderAsync(
            AITemplateIds.ExtractedDataAvailability,
            new Dictionary<string, object>
            {
                ["collectedFields"] = collectedFields,
                ["missingFields"] = missingFields,
            });

        if (string.IsNullOrEmpty(header))
        {
            return;
        }

        context.OrchestrationContext.SystemMessageBuilder.AppendLine();
        context.OrchestrationContext.SystemMessageBuilder.Append(header);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "Injected extracted session state for AIProfile '{ProfileId}' with {CollectedCount} collected field(s) and {MissingCount} missing field(s).",
                profile.ItemId,
                collectedFields.Count,
                missingFields.Count);
        }
    }
}
