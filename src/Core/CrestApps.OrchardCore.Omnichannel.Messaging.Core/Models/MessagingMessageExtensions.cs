using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Reads and writes the <see cref="MessagingMessageDetails"/> kept on a message's property bag.
/// </summary>
public static class MessagingMessageExtensions
{
    /// <summary>
    /// Gets the subject line of the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The subject, or <see langword="null"/> when the message has none.</returns>
    public static string GetSubject(this OmnichannelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.TryGet<MessagingMessageDetails>(out var details) && !string.IsNullOrWhiteSpace(details.Subject)
            ? details.Subject
            : null;
    }

    /// <summary>
    /// Sets the subject line of the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="subject">The subject; empty clears it.</param>
    public static void SetSubject(this OmnichannelMessage message, string subject)
    {
        ArgumentNullException.ThrowIfNull(message);

        Alter(message, details => details.Subject = MessagingSubjects.Clean(subject));
    }

    /// <summary>
    /// Gets the history the message quoted below its reply.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The quoted text, or <see langword="null"/> when the message quoted nothing.</returns>
    public static string GetQuotedText(this OmnichannelMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.TryGet<MessagingMessageDetails>(out var details) && !string.IsNullOrWhiteSpace(details.QuotedText)
            ? details.QuotedText
            : null;
    }

    /// <summary>
    /// Sets the history the message quoted below its reply.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="quotedText">The quoted text; empty clears it.</param>
    public static void SetQuotedText(this OmnichannelMessage message, string quotedText)
    {
        ArgumentNullException.ThrowIfNull(message);

        Alter(message, details => details.QuotedText = string.IsNullOrWhiteSpace(quotedText) ? null : quotedText);
    }

    private static void Alter(OmnichannelMessage message, Action<MessagingMessageDetails> alter)
    {
        var details = message.GetOrCreate<MessagingMessageDetails>();

        alter(details);

        message.Put(details);
    }
}
