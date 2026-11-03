using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Handlers;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Handlers;
using CrestApps.OrchardCore.AI.Core.Conversations;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Handlers;

/// <summary>
/// Tells the conversation turn handlers about a chat's latest exchange: what the visitor asked and what the AI
/// answered. Phone calls and text threads report their turns as they save them.
/// </summary>
internal sealed class ConversationTurnChatSessionHandler : AIChatSessionHandlerBase
{
    private readonly IAIConversationTurnNotifier _turnNotifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConversationTurnChatSessionHandler"/> class.
    /// </summary>
    /// <param name="turnNotifier">The turn notifier.</param>
    public ConversationTurnChatSessionHandler(IAIConversationTurnNotifier turnNotifier)
    {
        _turnNotifier = turnNotifier;
    }

    /// <inheritdoc/>
    public override async Task MessageCompletedAsync(ChatMessageCompletedContext context, CancellationToken cancellationToken = default)
    {
        var prompts = context.Prompts?
            .Where(prompt => !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content))
            .OrderBy(prompt => prompt.CreatedUtc)
            .ToList();

        if (prompts is not { Count: > 0 })
        {
            return;
        }

        var answer = prompts[^1].Role == ChatRole.Assistant ? prompts[^1] : null;
        var question = prompts.LastOrDefault(prompt => prompt.Role == ChatRole.User);

        if (question is not null)
        {
            await _turnNotifier.NotifyAsync(context.ChatSession, AIConversationTurn.CustomerRole, question.Content, cancellationToken);
        }

        if (answer is not null)
        {
            await _turnNotifier.NotifyAsync(context.ChatSession, AIConversationTurn.AssistantRole, answer.Content, cancellationToken);
        }
    }
}
