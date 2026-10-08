using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Services;

/// <summary>
/// Moves identity between the automated conversation's prompts and the transcript a handoff carries into the
/// human thread. A customer's prompt remembers which provider message it was, and every transcript entry names
/// the prompt it came from, so the human thread can recognise a message it already holds instead of recording it
/// a second time.
/// </summary>
internal static class SmsHandoffTranscript
{
    /// <summary>
    /// Creates the prompt that stores a customer's inbound text in the automated conversation.
    /// </summary>
    /// <param name="sessionId">The automated conversation's session identifier.</param>
    /// <param name="message">The inbound message.</param>
    /// <param name="createdUtc">The time the prompt is stored.</param>
    /// <returns>The prompt to store.</returns>
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
            prompt.Put(new SmsInboundPromptSource { ProviderMessageId = message.ProviderMessageId });
        }

        return prompt;
    }

    /// <summary>
    /// Builds the handoff transcript from the automated conversation's prompts, oldest first.
    /// </summary>
    /// <param name="prompts">The conversation's prompts, without generated ones.</param>
    /// <returns>The transcript entries.</returns>
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
                ProviderMessageId = prompt.TryGet<SmsInboundPromptSource>(out var source)
                    ? source.ProviderMessageId
                    : null,
            })
            .ToList();
    }
}
