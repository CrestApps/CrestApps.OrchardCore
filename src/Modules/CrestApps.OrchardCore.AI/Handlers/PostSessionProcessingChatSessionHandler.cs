using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Workflows.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// An <see cref="IAIChatSessionHandler"/> that runs post-session processing tasks
/// after a chat session is closed. Triggers workflow events when processing completes.
/// </summary>
public sealed class PostSessionProcessingChatSessionHandler : IAIChatSessionHandler
{
    private readonly PostSessionProcessingService _postSessionProcessingService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public PostSessionProcessingChatSessionHandler(
        PostSessionProcessingService postSessionProcessingService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<PostSessionProcessingChatSessionHandler> logger)
    {
        _postSessionProcessingService = postSessionProcessingService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        // Only process when the session has just been closed.
        if (context.ChatSession.Status != ChatSessionStatus.Closed)
        {
            return;
        }

        // Skip if post-session results have already been processed.
        if (context.ChatSession.PostSessionResults.Count > 0)
        {
            return;
        }

        try
        {
            var results = await _postSessionProcessingService.ProcessAsync(
                context.Profile,
                context.ChatSession);

            if (results is null || results.Count == 0)
            {
                return;
            }

            context.ChatSession.PostSessionResults = results;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Post-session processing completed for session '{SessionId}' with {TaskCount} results.",
                    context.ChatSession.SessionId,
                    results.Count);
            }

            var workflowManager = _serviceProvider.GetService<IWorkflowManager>();

            if (workflowManager is not null)
            {
                await TriggerPostProcessedEventAsync(workflowManager, context);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-session processing failed for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }

    private async Task TriggerPostProcessedEventAsync(
        IWorkflowManager workflowManager,
        ChatMessageCompletedContext context)
    {
        try
        {
            var input = new Dictionary<string, object>
            {
                { "SessionId", context.ChatSession.SessionId },
                { "ProfileId", context.Profile.ItemId },
                { "Results", context.ChatSession.PostSessionResults },
                { "Timestamp", _clock.UtcNow },
            };

            await workflowManager.TriggerEventAsync(
                nameof(AIChatSessionPostProcessedEvent),
                input,
                correlationId: context.ChatSession.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trigger AIChatSessionPostProcessedEvent for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
