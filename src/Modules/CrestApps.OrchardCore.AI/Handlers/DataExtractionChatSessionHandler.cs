using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Handlers;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Triggers Orchard workflow events from the shared data extraction results that
/// the framework handler stored on <see cref="ChatMessageCompletedContext.Items"/>.
/// </summary>
public sealed class DataExtractionChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly TimeProvider _timeProvider;
    private readonly IWorkflowManager _workflowManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataExtractionChatSessionHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to trigger workflow events.</param>
    /// <param name="timeProvider">The time provider for obtaining UTC timestamps.</param>
    /// <param name="logger">The logger instance for this handler.</param>
    public DataExtractionChatSessionHandler(
        IWorkflowManager workflowManager,
        TimeProvider timeProvider,
        ILogger<DataExtractionChatSessionHandler> logger)
    {
        _workflowManager = workflowManager;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Items.TryGetValue(AIChatSessionHandlerContextKeys.DataExtractionChangeSet, out var value)
            || value is not ExtractionChangeSet changeSet)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        foreach (var field in changeSet.NewFields)
        {
            await TriggerFieldExtractedEventAsync(context, field, now);
        }

        await TriggerAllFieldsExtractedEventAsync(context, now);

        if (changeSet.SessionEnded && context.ChatSession.Status == ChatSessionStatus.Closed)
        {
            await TriggerSessionClosedEventAsync(context, context.ChatSession.ClosedAtUtc ?? now);
        }
    }

    private async Task TriggerFieldExtractedEventAsync(
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
                { "Session", context.ChatSession },
                { "Profile", context.Profile },
                { "FieldName", field.FieldName },
                { "Value", field.Value },
                { "IsMultiple", field.IsMultiple },
                { "Timestamp", now },
            };

            await _workflowManager.TriggerEventAsync(
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
        ChatMessageCompletedContext context,
        DateTime now)
    {
        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", context.ChatSession.SessionId },
                { "ProfileId", context.Profile.ItemId },
                { "Session", context.ChatSession },
                { "Profile", context.Profile },
                { "ClosedAtUtc", now },
            };

            await _workflowManager.TriggerEventAsync(
                nameof(AIChatSessionClosedEvent),
                input,
                correlationId: context.ChatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger AIChatSessionClosedEvent for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }

    private async Task TriggerAllFieldsExtractedEventAsync(
        ChatMessageCompletedContext context,
        DateTime now)
    {
        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", context.ChatSession.SessionId },
                { "ProfileId", context.Profile.ItemId },
                { "Session", context.ChatSession },
                { "Profile", context.Profile },
                { "ExtractedData", context.ChatSession.ExtractedData },
                { "Timestamp", now },
            };

            await _workflowManager.TriggerEventAsync(
                nameof(AIChatSessionAllFieldsExtractedEvent),
                input,
                correlationId: context.ChatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger AIChatSessionAllFieldsExtractedEvent for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
