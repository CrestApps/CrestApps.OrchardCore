using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Collapses repeated rows of one message when a thread is read. New writes already record each message once;
/// this keeps threads written before that from showing a message twice, without rewriting their data.
/// </summary>
internal static class MessagingThreadDeduplicator
{
    /// <summary>
    /// How far apart the live row and the handoff's copy of the same customer text can be stamped. Both are
    /// taken from the clock while the one delivery is being processed.
    /// </summary>
    private static readonly TimeSpan _copyWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns the thread's messages with repeated rows of one message removed, keeping the first of each and
    /// the original order.
    /// </summary>
    /// <param name="messages">The thread's messages, in display order.</param>
    /// <returns>The messages to display.</returns>
    public static IReadOnlyList<OmnichannelMessage> Collapse(IReadOnlyList<OmnichannelMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        // A handoff copy of a customer text was written without the provider id the live row has. The only way an
        // inbound row with no provider id shares its words and its moment with one that has an id is that it is a
        // copy of it: two genuine texts each carry their own id. Each live row absorbs at most one copy.
        var liveTwins = messages
            .Where(message => message.IsInbound && !string.IsNullOrEmpty(message.ProviderMessageId))
            .ToList();

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenProviderMessageIds = new HashSet<string>(StringComparer.Ordinal);
        var collapsed = new List<OmnichannelMessage>(messages.Count);

        foreach (var message in messages)
        {
            if (!string.IsNullOrEmpty(message.ProviderMessageId) && !seenProviderMessageIds.Add(message.ProviderMessageId))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(message.Id) && !seenIds.Add(message.Id))
            {
                continue;
            }

            if (IsCopyOfLiveRow(message, liveTwins))
            {
                continue;
            }

            collapsed.Add(message);
        }

        return collapsed;
    }

    private static bool IsCopyOfLiveRow(OmnichannelMessage message, List<OmnichannelMessage> liveTwins)
    {
        if (!message.IsInbound || !string.IsNullOrEmpty(message.ProviderMessageId))
        {
            return false;
        }

        var content = message.Content?.Trim();

        var twin = liveTwins.FindIndex(live =>
            string.Equals(live.Content?.Trim(), content, StringComparison.Ordinal) &&
            (live.CreatedUtc - message.CreatedUtc).Duration() <= _copyWindow);

        if (twin < 0)
        {
            return false;
        }

        liveTwins.RemoveAt(twin);

        return true;
    }
}
