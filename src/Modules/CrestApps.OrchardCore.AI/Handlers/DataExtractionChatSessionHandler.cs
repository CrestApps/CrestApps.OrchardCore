using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// An <see cref="IAIChatSessionHandler"/> that runs data extraction after each
/// message exchange, triggers workflow events for newly extracted fields, and
/// closes the session when a natural farewell is detected.
/// </summary>
public sealed class DataExtractionChatSessionHandler : IAIChatSessionHandler
{
    private readonly DataExtractionService _dataExtractionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DataExtractionChatSessionHandler(
        DataExtractionService dataExtractionService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<DataExtractionChatSessionHandler> logger)
    {
        _dataExtractionService = dataExtractionService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        try
        {
            var changeSet = await _dataExtractionService.ProcessAsync(
                context.Profile,
                context.ChatSession);

            if (changeSet is null)
            {
                return;
            }

            var now = _clock.UtcNow;
            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            // Trigger workflow events for each newly extracted field.
            if (workflowManager is not null)
            {
                foreach (var field in changeSet.NewFields)
                {
                    await TriggerFieldExtractedEventAsync(workflowManager, context, field, now);
                }
            }

            // Close the session if the model detected a natural farewell.
            if (changeSet.SessionEnded && context.ChatSession.Status != ChatSessionStatus.Closed)
            {
                context.ChatSession.Status = ChatSessionStatus.Closed;
                context.ChatSession.ClosedAtUtc = now;

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Session '{SessionId}' closed due to natural conversation ending.",
                        context.ChatSession.SessionId);
                }

                if (workflowManager is not null)
                {
                    await TriggerSessionClosedEventAsync(workflowManager, context, now);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data extraction failed for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }

    private async Task TriggerFieldExtractedEventAsync(
        IWorkflowManager workflowManager,
        ChatMessageCompletedContext context,
        ExtractedFieldChange field,
        DateTime now)
    {
        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", context.ChatSession.SessionId },
                { "ProfileId", context.Profile.ItemId },
                { "FieldName", field.FieldName },
                { "Value", field.Value },
                { "IsMultiple", field.IsMultiple },
                { "Timestamp", now },
            };

            await workflowManager.TriggerEventAsync(
                nameof(AIChatSessionFieldExtractedEvent),
                input,
                correlationId: context.ChatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger AIChatSessionFieldExtractedEvent for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }

    private async Task TriggerSessionClosedEventAsync(
        IWorkflowManager workflowManager,
        ChatMessageCompletedContext context,
        DateTime now)
    {
        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", context.ChatSession.SessionId },
                { "ProfileId", context.Profile.ItemId },
                { "ClosedAtUtc", now },
            };

            await workflowManager.TriggerEventAsync(
                nameof(AIChatSessionClosedEvent),
                input,
                correlationId: context.ChatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger AIChatSessionClosedEvent for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
