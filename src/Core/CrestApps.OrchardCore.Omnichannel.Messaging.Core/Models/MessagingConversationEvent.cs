namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// What happened to a conversation in a <see cref="MessagingConversationEvent"/>.
/// </summary>
public enum MessagingConversationEventKind
{
    /// <summary>
    /// The conversation was handed to another person, or sent back to a team's shared pool.
    /// </summary>
    Transferred,
}

/// <summary>
/// One entry in a conversation's own history, such as a transfer. It is kept on the conversation rather than as a
/// message, so it is never sent to the customer, never counted as unread and never read back as part of the
/// customer's transcript, yet the thread can still show it where it happened. The names are captured when the event
/// is recorded, so the history reads the way it did at the time even after someone is renamed or leaves.
/// </summary>
public sealed class MessagingConversationEvent
{
    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public MessagingConversationEventKind Kind { get; set; }

    /// <summary>
    /// Gets or sets when it happened, in UTC.
    /// </summary>
    public DateTime OccurredUtc { get; set; }

    /// <summary>
    /// Gets or sets the agent profile of the person who made the change, when a person made it.
    /// </summary>
    public string ActorAgentId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the person who made the change.
    /// </summary>
    public string ActorName { get; set; }

    /// <summary>
    /// Gets or sets the agent who held the conversation before, or <see langword="null"/> when nobody held it.
    /// </summary>
    public string FromAgentId { get; set; }

    /// <summary>
    /// Gets or sets the display name of whoever held the conversation before: the agent, or the team whose shared
    /// pool it sat in.
    /// </summary>
    public string FromName { get; set; }

    /// <summary>
    /// Gets or sets the agent the conversation went to, when it went to a person.
    /// </summary>
    public string ToAgentId { get; set; }

    /// <summary>
    /// Gets or sets the queue (team) the conversation went to, when it was sent back to a shared pool.
    /// </summary>
    public string ToQueueId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the person or team the conversation went to.
    /// </summary>
    public string ToName { get; set; }

    /// <summary>
    /// Gets or sets the internal note left for the recipient. It is shown to the people working the conversation and
    /// never sent to the customer.
    /// </summary>
    public string Note { get; set; }
}
