using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The thread roll-up the inbox renders: the preview line, the unread count, and the stamps. It was computed in
/// four places with four slightly different truncation rules, so the same message read differently depending on
/// which path wrote it.
/// </summary>
public static class SmsConversationRollup
{
    /// <summary>
    /// The number of characters of a message the inbox row shows.
    /// </summary>
    public const int PreviewLength = 120;

    /// <summary>
    /// Builds the inbox preview line for a message body.
    /// </summary>
    /// <param name="content">The message body.</param>
    /// <returns>The preview, never <see langword="null"/>, because the inbox row binds it directly.</returns>
    public static string BuildPreview(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var normalized = content.ReplaceLineEndings(" ").Trim();

        return normalized.Length <= PreviewLength
            ? normalized
            : normalized[..PreviewLength];
    }

    /// <summary>
    /// Rolls a received message (or an imported transcript) up onto the thread and marks it unread.
    /// </summary>
    /// <param name="conversation">The conversation to roll up.</param>
    /// <param name="content">The body the preview is built from.</param>
    /// <param name="messageUtc">When the message was received.</param>
    /// <param name="nowUtc">The current instant.</param>
    /// <param name="unreadIncrement">How many unread messages this pass adds; at least one.</param>
    public static void ApplyInbound(SmsConversation conversation, string content, DateTime messageUtc, DateTime nowUtc, int unreadIncrement = 1)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        conversation.LastMessageUtc = messageUtc == default ? nowUtc : messageUtc;
        conversation.LastMessagePreview = BuildPreview(content);
        conversation.UnreadCount += Math.Max(1, unreadIncrement);
        conversation.IsRead = false;
        conversation.ModifiedUtc = nowUtc;
    }

    /// <summary>
    /// Rolls a sent message up onto the thread. The unread count is untouched: the agent is the one sending, so
    /// their own message cannot make the thread more unread to them.
    /// </summary>
    /// <param name="conversation">The conversation to roll up.</param>
    /// <param name="content">The body the preview is built from.</param>
    /// <param name="messageUtc">When the message was sent.</param>
    /// <param name="nowUtc">The current instant.</param>
    public static void ApplyOutbound(SmsConversation conversation, string content, DateTime messageUtc, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        conversation.LastMessageUtc = messageUtc == default ? nowUtc : messageUtc;
        conversation.LastMessagePreview = BuildPreview(content);
        conversation.ModifiedUtc = nowUtc;
    }
}
