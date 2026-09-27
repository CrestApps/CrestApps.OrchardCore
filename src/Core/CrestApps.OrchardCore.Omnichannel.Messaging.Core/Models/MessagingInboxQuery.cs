using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Describes one page of the messaging inbox: who is asking, which threads they may see, which tab they are on, and
/// where in the ordered result the page starts. The store turns it into a single indexed query, so the inbox no
/// longer loads every conversation in the tenant and filters in memory.
/// </summary>
public sealed class MessagingInboxQuery
{
    /// <summary>
    /// Gets or sets the identifier of the agent asking. Threads assigned to them, and personal threads they own,
    /// are always visible.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent serves. Threads owned by one of them are visible unless another agent
    /// has already taken them.
    /// </summary>
    public IReadOnlyCollection<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the caller may see every thread in the tenant, which is what the
    /// supervisor permission grants.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the inbox tab being shown.
    /// </summary>
    public MessagingInboxFilter Filter { get; set; } = MessagingInboxFilter.All;

    /// <summary>
    /// Gets or sets the channel to narrow the inbox to, or <see langword="null"/> for every channel.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether only threads nobody has read since they last changed are matched: a
    /// customer message no one has opened, or a conversation handed over that its recipient has not looked at yet.
    /// The Messaging menu counts these, so the number goes down as soon as the agent opens what it points at.
    /// </summary>
    public bool UnreadOnly { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether only open threads are matched. A snoozed, closed or spam thread is
    /// not waiting on anyone, even when it still carries unread messages.
    /// </summary>
    public bool OpenOnly { get; set; }

    /// <summary>
    /// Gets or sets how many conversations to skip before the page starts.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many conversations the page holds. Zero or less means "no bound", which only the count
    /// query uses.
    /// </summary>
    public int Take { get; set; }
}
