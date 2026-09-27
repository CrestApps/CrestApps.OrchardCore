using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Lays a conversation's history (its transfers) in among its messages, in the order things happened, so the thread
/// shows "transferred to …" at the point it happened rather than in a list of its own.
/// </summary>
public static class ThreadTimeline
{
    /// <summary>
    /// Picks the history entries that belong with one page of messages. A thread loads its newest page first, so an
    /// entry older than that page waits for the earlier page that holds the messages around it.
    /// </summary>
    /// <param name="history">The conversation's history, in any order.</param>
    /// <param name="messages">The page of messages, oldest first.</param>
    /// <param name="beforeUtc">Where the page ends when it is an earlier page, or <see langword="null"/> for the newest.</param>
    /// <param name="hasEarlierMessages">Whether there are messages before this page.</param>
    /// <returns>The entries of the page, oldest first.</returns>
    public static IReadOnlyList<MessagingConversationEvent> ForPage(
        IEnumerable<MessagingConversationEvent> history,
        IReadOnlyList<OmnichannelMessage> messages,
        DateTime? beforeUtc,
        bool hasEarlierMessages)
    {
        if (history is null)
        {
            return [];
        }

        var from = hasEarlierMessages && messages is { Count: > 0 } ? messages[0].CreatedUtc : DateTime.MinValue;

        return history
            .Where(entry => entry is not null &&
                entry.OccurredUtc >= from &&
                (!beforeUtc.HasValue || entry.OccurredUtc < beforeUtc.Value))
            .OrderBy(entry => entry.OccurredUtc)
            .ToArray();
    }

    /// <summary>
    /// Merges messages and history entries into one list in the order they happened. A message and an entry recorded
    /// at the same moment keep the message first.
    /// </summary>
    /// <param name="messages">The messages, oldest first.</param>
    /// <param name="events">The history entries.</param>
    /// <returns>The timeline.</returns>
    public static IReadOnlyList<ThreadTimelineItem> Merge(
        IReadOnlyList<OmnichannelMessage> messages,
        IEnumerable<MessagingConversationEvent> events)
    {
        var pending = new Queue<MessagingConversationEvent>((events ?? []).Where(entry => entry is not null).OrderBy(entry => entry.OccurredUtc));
        var items = new List<ThreadTimelineItem>();

        foreach (var message in messages ?? [])
        {
            while (pending.Count > 0 && pending.Peek().OccurredUtc < message.CreatedUtc)
            {
                items.Add(new ThreadTimelineItem(null, pending.Dequeue()));
            }

            items.Add(new ThreadTimelineItem(message, null));
        }

        while (pending.Count > 0)
        {
            items.Add(new ThreadTimelineItem(null, pending.Dequeue()));
        }

        return items;
    }

    /// <summary>
    /// Words a transfer as the thread shows it, from the names captured when it happened. A queue is named as one, so
    /// "to Billing" is not read as a person called Billing, and whoever made the transfer is only named apart from the
    /// sender when it was somebody else, such as a supervisor.
    /// </summary>
    /// <param name="entry">The transfer.</param>
    /// <param name="T">The localizer of the view that shows it.</param>
    /// <returns>The sentence. The names are its arguments, so they are encoded once, when it is written.</returns>
    public static LocalizedHtmlString DescribeTransfer(MessagingConversationEvent entry, IHtmlLocalizer T)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(T);

        // GetString formats the destination as plain text; it is encoded as an argument of the sentence.
        var to = !string.IsNullOrEmpty(entry.ToQueueId)
            ? T.GetString("the {0} queue", string.IsNullOrWhiteSpace(entry.ToName) ? T.GetString("unknown").Value : entry.ToName).Value
            : string.IsNullOrWhiteSpace(entry.ToName) ? T.GetString("another agent").Value : entry.ToName;
        var from = entry.FromName;
        var actor = entry.ActorName;
        var actorIsSender = !string.IsNullOrEmpty(entry.ActorAgentId) && string.Equals(entry.ActorAgentId, entry.FromAgentId, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(from))
        {
            return string.IsNullOrWhiteSpace(actor)
                ? T["Transferred to {0}", to]
                : T["{0} transferred this conversation to {1}", actor, to];
        }

        return string.IsNullOrWhiteSpace(actor) || actorIsSender
            ? T["Transferred from {0} to {1}", from, to]
            : T["{0} transferred this conversation from {1} to {2}", actor, from, to];
    }
}

/// <summary>
/// One entry of a thread: a message, or a history entry such as a transfer.
/// </summary>
/// <param name="Message">The message, when the entry is one.</param>
/// <param name="Event">The history entry, when the entry is one.</param>
public sealed record ThreadTimelineItem(OmnichannelMessage Message, MessagingConversationEvent Event)
{
    /// <summary>
    /// Gets when the entry happened, in UTC.
    /// </summary>
    public DateTime OccurredUtc => Message?.CreatedUtc ?? Event.OccurredUtc;
}
