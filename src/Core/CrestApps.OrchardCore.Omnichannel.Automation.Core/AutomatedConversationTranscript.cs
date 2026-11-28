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
    /// The longest subject passed to the model with the customer's message. The sender writes the subject, so it is kept
    /// to what a subject line holds rather than letting it fill the turn.
    /// </summary>
    internal const int MaxSubjectLength = 200;

    // Collapsed in a subject so it reads as one line of what the customer wrote and cannot pose as separate turns.
    private static readonly char[] _subjectBreaks = ['\r', '\n', '\t'];

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
    /// opening message is stored as an assistant turn, so this holds from the very first turn. A customer turn the agent
    /// handled on purpose without replying ends the search like a reply does, so it and the turns before it are not owed.
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
                if (IsUnanswered(prompt))
                {
                    break;
                }

                pending.Add(prompt);
            }
        }

        pending.Reverse();

        return pending;
    }

    /// <summary>
    /// Determines whether the agent is waiting on the customer: something was said, and no customer message is owed a
    /// reply, because the agent spoke last or handled the customer's last message without replying.
    /// </summary>
    /// <param name="conversation">The transcript, oldest first.</param>
    /// <returns><see langword="true"/> when the next message is the customer's to send.</returns>
    internal static bool IsAwaitingCustomer(IReadOnlyList<AIChatSessionPrompt> conversation)
        => conversation is { Count: > 0 } && GetTrailingUserMessages(conversation).Count == 0;

    /// <summary>
    /// Determines whether the transcript already holds the customer's message with the provider's identifier. A provider
    /// redelivers or replays a message under the same identifier, and the replay must not be recorded or answered again.
    /// </summary>
    /// <param name="conversation">The transcript.</param>
    /// <param name="providerMessageId">The provider's identifier of the inbound message.</param>
    /// <returns><see langword="true"/> when a customer turn already carries the identifier.</returns>
    internal static bool HasProviderMessage(IEnumerable<AIChatSessionPrompt> conversation, string providerMessageId)
    {
        if (conversation is null || string.IsNullOrEmpty(providerMessageId))
        {
            return false;
        }

        return conversation.Any(prompt =>
            prompt.Role == ChatRole.User &&
            prompt.TryGet<AutomatedInboundPromptSource>(out var source) &&
            string.Equals(source.ProviderMessageId, providerMessageId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Counts the agent's messages since a moment: replies, follow-ups and the opening message alike.
    /// </summary>
    /// <param name="conversation">The transcript.</param>
    /// <param name="sinceUtc">The start of the window.</param>
    /// <returns>The number of the agent's messages sent at or after <paramref name="sinceUtc"/>.</returns>
    internal static int CountAgentMessagesSince(IEnumerable<AIChatSessionPrompt> conversation, DateTime sinceUtc)
        => conversation?.Count(prompt => prompt.Role == ChatRole.Assistant && prompt.CreatedUtc >= sinceUtc) ?? 0;

    /// <summary>
    /// Marks a customer turn the agent handled on purpose without replying, so the owed-reply recovery does not raise it
    /// again and the follow-ups treat the agent as waiting on the customer.
    /// </summary>
    /// <param name="prompt">The customer's turn.</param>
    /// <param name="reason">Why no reply was sent.</param>
    /// <param name="utcNow">The current time.</param>
    internal static void MarkUnanswered(AIChatSessionPrompt prompt, string reason, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        prompt.Put(new AutomatedUnansweredTurn { Reason = reason, HandledUtc = utcNow });
    }

    /// <summary>
    /// Determines whether a customer turn was handled on purpose without a reply.
    /// </summary>
    /// <param name="prompt">The turn.</param>
    /// <returns><see langword="true"/> when the turn carries the mark.</returns>
    internal static bool IsUnanswered(AIChatSessionPrompt prompt)
        => prompt is not null && prompt.Has<AutomatedUnansweredTurn>();

    /// <summary>
    /// Puts the subject the customer wrote under in front of their latest message in the transcript sent to the model.
    /// The subject is the customer's own words, so it belongs in their turn: as an instruction it would let anyone who
    /// emails the address steer the agent.
    /// </summary>
    /// <param name="transcript">The transcript sent to the model; only this in-memory copy changes.</param>
    /// <param name="subject">The subject of the customer's latest message, if the channel has subjects.</param>
    internal static void PrefixSubject(List<ChatMessage> transcript, string subject)
    {
        ArgumentNullException.ThrowIfNull(transcript);

        var clean = CleanSubject(subject);

        if (string.IsNullOrEmpty(clean))
        {
            return;
        }

        var index = transcript.FindLastIndex(message => message.Role == ChatRole.User);

        if (index < 0)
        {
            return;
        }

        transcript[index] = new ChatMessage(ChatRole.User, $"Subject: {clean}\n\n{transcript[index].Text}");
    }

    /// <summary>
    /// Brings a subject to one line of at most <see cref="MaxSubjectLength"/> characters.
    /// </summary>
    /// <param name="subject">The subject as received.</param>
    /// <returns>The subject on one line, or <see langword="null"/> when there is none.</returns>
    internal static string CleanSubject(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var oneLine = string.Join(' ', subject.Split(_subjectBreaks, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return oneLine.Length > MaxSubjectLength
            ? oneLine[..MaxSubjectLength].TrimEnd()
            : oneLine;
    }
}
