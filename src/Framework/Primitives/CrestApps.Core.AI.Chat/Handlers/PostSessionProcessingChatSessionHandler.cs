using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Handlers;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.Core.AI.Chat.Handlers;

/// <summary>
/// Runs the shared post-close processing pipeline immediately after a chat
/// session transitions to the closed state.
/// </summary>
public sealed class PostSessionProcessingChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly AIChatSessionPostCloseProcessor _postCloseProcessor;
    private readonly ILogger<PostSessionProcessingChatSessionHandler> _logger;

    public PostSessionProcessingChatSessionHandler(
        AIChatSessionPostCloseProcessor postCloseProcessor,
        ILogger<PostSessionProcessingChatSessionHandler> logger)
    {
        _postCloseProcessor = postCloseProcessor;
        _logger = logger;
    }

    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context)
    {
        if (context.ChatSession.Status != ChatSessionStatus.Closed)
        {
            return;
        }

        try
        {
            var result = await _postCloseProcessor.ProcessAsync(
                context.Profile,
                context.ChatSession,
                context.Prompts);

            context.Items[AIChatSessionHandlerContextKeys.PostCloseProcessingResult] = result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shared post-close processing failed for session '{SessionId}'.", context.ChatSession.SessionId);
        }
    }
}
