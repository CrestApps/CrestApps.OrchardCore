using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// The subject-line conventions shared by every channel whose messages carry a subject.
/// </summary>
public static class MessagingSubjects
{
    // The reply prefixes mail clients add, in the languages they are most often seen in. A subject that already
    // starts with one is a reply already, and gains no second prefix.
    private static readonly string[] _replyPrefixes = ["re:", "aw:", "sv:", "antw:", "réf:", "rif:", "odp:", "ynt:", "vs:"];

    /// <summary>
    /// The longest subject kept, which is well past what any mail client shows and short enough to store.
    /// </summary>
    public const int MaxLength = 255;

    /// <summary>
    /// Builds the subject of a reply to a message with the given subject: the subject with one <c>Re:</c> in front.
    /// </summary>
    /// <param name="subject">The subject of the message being answered.</param>
    /// <returns>The reply subject, or <see langword="null"/> when <paramref name="subject"/> is empty.</returns>
    public static string ForReply(string subject)
    {
        var trimmed = Clean(subject);

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return IsReply(trimmed)
            ? trimmed
            : Clean($"Re: {trimmed}");
    }

    /// <summary>
    /// Determines whether a subject is already a reply's.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <returns><see langword="true"/> when the subject starts with a reply prefix.</returns>
    public static bool IsReply(string subject)
        => !string.IsNullOrWhiteSpace(subject) &&
            _replyPrefixes.Any(prefix => subject.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Brings a subject into the form it is stored and sent in: one line, trimmed, bounded in length.
    /// </summary>
    /// <param name="subject">The subject as received or typed.</param>
    /// <returns>The cleaned subject, or <see langword="null"/> when it is empty.</returns>
    public static string Clean(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        // A folded header or a pasted subject can carry line breaks, which would let a subject inject a header.
        var oneLine = string.Join(' ', subject.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return oneLine.Length <= MaxLength
            ? oneLine
            : oneLine.Substring(0, MaxLength).TrimEnd();
    }

    /// <summary>
    /// Finds the subject a reply on a thread should carry: <c>Re:</c> and the subject of the newest message that has one.
    /// </summary>
    /// <param name="messages">The thread's messages, in any order.</param>
    /// <returns>The reply subject, or <see langword="null"/> when no message has a subject.</returns>
    public static string ForReplyTo(IEnumerable<OmnichannelMessage> messages)
    {
        var latest = messages?
            .Where(message => message is not null && !string.IsNullOrEmpty(message.GetSubject()))
            .OrderByDescending(message => message.CreatedUtc)
            .FirstOrDefault();

        return latest is null ? null : ForReply(latest.GetSubject());
    }
}
