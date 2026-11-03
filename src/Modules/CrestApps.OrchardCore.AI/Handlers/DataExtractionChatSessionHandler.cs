using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Handlers;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Triggers Orchard workflow events from the shared data extraction results that
/// the framework handler stored on <see cref="ChatMessageCompletedContext.Items"/>.
/// </summary>
public sealed class DataExtractionChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly IClock _clock;
    private readonly IWorkflowManager _workflowManager;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataExtractionChatSessionHandler"/> class.
    /// </summary>
    /// <param name="workflowManager">The workflow manager used to trigger workflow events.</param>
    /// <param name="clock">The time provider for obtaining UTC timestamps.</param>
    /// <param name="logger">The logger instance for this handler.</param>
    public DataExtractionChatSessionHandler(
        IWorkflowManager workflowManager,
        IClock clock,
        ILogger<DataExtractionChatSessionHandler> logger)
    {
        _workflowManager = workflowManager;
        _clock = clock;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Items.TryGetValue(AIChatSessionHandlerContextKeys.DataExtractionChangeSet, out var value)
            || value is not ExtractionChangeSet changeSet)
        {
            return;
        }

        var now = _clock.UtcNow;

        foreach (var field in changeSet.NewFields)
        {
            // What the AI captured is a conversation variable too, so the next tool call and the workflows read it.
            context.ChatSession.SetConversationVariable(field.FieldName, ToVariableValue(field.Value));

            await TriggerFieldExtractedEventAsync(context, field, now);
        }

        await TriggerAllFieldsExtractedEventAsync(context, now);

        if (changeSet.SessionEnded && context.ChatSession.Status == ChatSessionStatus.Closed)
        {
            await TriggerSessionClosedEventAsync(context, context.ChatSession.ClosedAtUtc ?? now);
        }
    }

    // An extracted value as a conversation variable: text as is, and several values as a comma-separated list.
    private static string ToVariableValue(object value)
        => value switch
        {
            null => null,
            string text => text,
            IEnumerable<string> values => string.Join(", ", values),
            _ => value.ToString(),
        };

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

            AIConversationTranscript.AddTo(input, context.ChatSession, prompts: null);

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

            AIConversationTranscript.AddTo(input, context.ChatSession, context.Prompts);

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
