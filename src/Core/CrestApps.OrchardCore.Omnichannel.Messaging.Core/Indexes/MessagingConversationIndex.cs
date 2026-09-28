using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;

/// <summary>
/// The YesSql index used to query <c>MessagingConversation</c> documents: find-or-create by channel, endpoint and contact address, list an
/// agent's or queue's inbox, gather a customer's conversations across channels, and filter by read/assignment/status.
/// </summary>
public sealed class MessagingConversationIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the channel the thread runs on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets our address (the endpoint) the thread runs on.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the contact's address on the channel.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets the linked contact content item, when the sender is a known contact.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the key of the customer behind the thread, shared by their conversations on every channel, so
    /// the workspace can find them all with one seek.
    /// </summary>
    public string CustomerKey { get; set; }

    /// <summary>
    /// Gets or sets the owner type (Personal or Queue), stored as its string name.
    /// </summary>
    public string OwnerType { get; set; }

    /// <summary>
    /// Gets or sets the owner identifier (agent profile id or queue id).
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the assigned agent.
    /// </summary>
    public string AssignedAgentId { get; set; }

    /// <summary>
    /// Gets or sets the assignment status, stored as its string name.
    /// </summary>
    public string AssignmentStatus { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle status, stored as its string name.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the thread has been read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the most recent message, used to order the inbox.
    /// </summary>
    public DateTime? LastMessageUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of unread inbound messages, so the inbox badge is read from the index rather than
    /// from every conversation document.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time a routed thread was push-assigned, cleared when the agent engages. Indexed so
    /// the pickup sweep can seek the threads that are actually overdue instead of filtering in memory.
    /// </summary>
    public DateTime? AssignedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a first reply is due, so the thirty-second sweep seeks the overdue threads rather than
    /// reading every open conversation and filtering them in memory.
    /// </summary>
    public DateTime? FirstResponseDueUtc { get; set; }
}
