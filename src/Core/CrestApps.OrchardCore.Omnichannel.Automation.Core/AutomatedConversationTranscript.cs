using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// Moves turns between an automated conversation's AI transcript and the messages a human thread holds.
/// </summary>
public static class AutomatedConversationTranscript
{
    /// <summary>
    /// Creates the transcript turn for a customer's message, remembering the provider's message identifier.
    /// </summary>
    /// <param name="sessionId">The AI chat session.</param>
    /// <param name="message">The customer's message.</param>
    /// <param name="createdUtc">When the turn is recorded.</param>
    /// <returns>The transcript turn.</returns>
    public static AIChatSessionPrompt CreateCustomerPrompt(string sessionId, OmnichannelMessage message, DateTime createdUtc)
    {
        ArgumentNullException.ThrowIfNull(message);

        var prompt = new AIChatSessionPrompt
        {
            ItemId = UniqueId.GenerateId(),
            SessionId = sessionId,
            Role = ChatRole.User,
            Content = message.Content,
            CreatedUtc = createdUtc,
        };

        if (!string.IsNullOrEmpty(message.ProviderMessageId))
        {
            prompt.Put(new AutomatedInboundPromptSource { ProviderMessageId = message.ProviderMessageId });
        }

        return prompt;
    }

    /// <summary>
    /// Builds the messages a handoff copies into the human thread: each turn with its identifier and, for the customer's
    /// turns, the provider message identifier, so the thread recognises a message it already holds.
    /// </summary>
    /// <param name="prompts">The transcript.</param>
    /// <returns>The handoff messages.</returns>
    public static List<OmnichannelHandoffMessage> Build(IEnumerable<AIChatSessionPrompt> prompts)
    {
        ArgumentNullException.ThrowIfNull(prompts);

        return prompts
            .Select(prompt => new OmnichannelHandoffMessage
            {
                Id = prompt.ItemId,
                IsInbound = prompt.Role == ChatRole.User,
                Content = prompt.Content,
                CreatedUtc = prompt.CreatedUtc,
                ProviderMessageId = prompt.TryGet<AutomatedInboundPromptSource>(out var source)
                    ? source.ProviderMessageId
                    : null,
            })
            .ToList();
    }

    /// <summary>
    /// Gets the customer messages that trail the transcript after the last assistant reply: the messages the automated
    /// agent has not answered yet. An empty result means the last thing said was the agent's own, so nothing is owed. The
    /// opening message is stored as an assistant turn, so this holds from the very first turn.
    /// </summary>
    /// <param name="conversation">The transcript, oldest first.</param>
    /// <returns>The unanswered customer turns, oldest first.</returns>
    public static List<AIChatSessionPrompt> GetTrailingUserMessages(IReadOnlyList<AIChatSessionPrompt> conversation)
    {
        var pending = new List<AIChatSessionPrompt>();

        if (conversation is null)
        {
            return pending;
        }

        for (var index = conversation.Count - 1; index >= 0; index--)
        {
            var prompt = conversation[index];

            if (prompt.Role == ChatRole.Assistant)
            {
                break;
            }

            if (prompt.Role == ChatRole.User)
            {
                pending.Add(prompt);
            }
        }

        pending.Reverse();

        return pending;
    }
}
