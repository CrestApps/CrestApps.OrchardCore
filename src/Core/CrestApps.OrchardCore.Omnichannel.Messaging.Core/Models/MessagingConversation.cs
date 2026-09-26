using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// One two-way conversation in the messaging workspace: a thread on one channel (<see cref="Channel"/>) between
/// one of our endpoints (<see cref="ServiceAddress"/>) and a contact (<see cref="ContactAddress"/>). Stored as its
/// own document via <c>ICatalog&lt;MessagingConversation&gt;</c>. The document holds only the thread rollup; the
/// message bodies live as individual <c>OmnichannelMessage</c> records linked by an indexed <c>ConversationId</c>.
/// A customer reached on several channels has one conversation per channel, tied together by
/// <see cref="GetCustomerKey()"/>, which is what lets the workspace show them side by side.
/// </summary>
public sealed class MessagingConversation : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the technical name of the messaging channel the thread runs on, such as <c>SMS</c>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets our address the thread runs on, in the channel's normalized form: a number we own for SMS, a
    /// mailbox for email. It selects the endpoint, and so the routing and the provider.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the contact's address on the channel, in the channel's normalized form.
    /// </summary>
    public string ContactAddress { get; set; }

    /// <summary>
    /// Gets or sets who owns the thread: a personal agent inbox or a queue (department).
    /// </summary>
    public ConversationOwnerType OwnerType { get; set; } = ConversationOwnerType.Personal;

    /// <summary>
    /// Gets or sets the owner identifier: an agent profile id for <see cref="ConversationOwnerType.Personal"/>,
    /// or an <c>ActivityQueue</c> id for <see cref="ConversationOwnerType.Queue"/>.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the resolved Omnichannel contact content item, or <see langword="null"/>
    /// for an unknown contact.
    /// </summary>
    public string ContactContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent currently assigned to the thread.
    /// </summary>
    public string AssignedAgentId { get; set; }

    /// <summary>
    /// Gets or sets whether and how the thread is assigned.
    /// </summary>
    public ConversationAssignmentStatus AssignmentStatus { get; set; } = ConversationAssignmentStatus.Unassigned;

    /// <summary>
    /// Gets or sets the lifecycle status of the thread.
    /// </summary>
    public ConversationStatus Status { get; set; } = ConversationStatus.Open;

    /// <summary>
    /// Gets or sets a value indicating whether the thread has been read.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Gets or sets the number of unread inbound messages.
    /// </summary>
    public int UnreadCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the most recent message in the thread.
    /// </summary>
    public DateTime? LastMessageUtc { get; set; }

    /// <summary>
    /// Gets or sets a short preview of the most recent message body.
    /// </summary>
    public string LastMessagePreview { get; set; }

    /// <summary>
    /// Gets or sets the AI chat session id when the thread was, or still is, AI-handled. Set on handoff so the
    /// human thread can hydrate the prior automated transcript.
    /// </summary>
    public string AISessionId { get; set; }

    /// <summary>
    /// Gets or sets a short AI-written summary of the automated conversation, captured at handoff so the agent
    /// taking over sees why the customer was transferred and what the AI learned without reading the whole thread.
    /// A single summary is kept: if the same customer is re-engaged and handed off again later, the newer summary
    /// replaces this one (see <see cref="SummaryGeneratedUtc"/> for when the current one was written).
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the current <see cref="Summary"/> was generated. Shown next to the summary so the
    /// agent knows how recent it is, since a later handoff of the same thread overwrites the summary in place.
    /// </summary>
    public DateTime? SummaryGeneratedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the thread was routed (push-assigned) to <see cref="AssignedAgentId"/>. Used by
    /// the reassignment sweep to detect a routed conversation the assigned agent has not picked up in time.
    /// </summary>
    public DateTime? AssignedUtc { get; set; }

    /// <summary>
    /// Gets or sets the number of times a routed conversation has been re-routed to another agent after not being
    /// picked up. Bounds the re-routing so a thread ignored by successive agents falls back to the shared pool
    /// instead of bouncing between them forever. Reset to zero once an agent engages.
    /// </summary>
    public int ReassignmentAttempts { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the conversation was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the conversation was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a first reply is due, set when the thread is placed on a queue that has a target. Null
    /// once someone has replied, or when the queue has no target.
    /// </summary>
    public DateTime? FirstResponseDueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the first reply was actually sent.
    /// </summary>
    public DateTime? FirstRespondedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the first-response target was missed and that has been announced.
    /// The flag is what stops the thirty-second sweep re-announcing the same breach until a supervisor learns to
    /// ignore the signal.
    /// </summary>
    public bool FirstResponseBreached { get; set; }

    /// <summary>
    /// Gets or sets when the endpoint's auto-reply was last sent on this thread, so a contact who sends three
    /// messages in a row gets one acknowledgement rather than three.
    /// </summary>
    public DateTime? LastAutoReplyUtc { get; set; }

    /// <summary>
    /// Gets or sets the conversation's own history, oldest first: the transfers it went through, with who made them
    /// and any note left for the recipient. Bounded, so a thread passed around for years does not grow without end.
    /// </summary>
    public IList<MessagingConversationEvent> History { get; set; } = [];

    /// <summary>
    /// Gets the agent holding the thread now: its assignee, or the owner of a personal thread nobody is assigned to.
    /// </summary>
    /// <returns>The agent profile identifier, or <see langword="null"/> when nobody holds the thread.</returns>
    public string GetHolderAgentId()
    {
        if (AssignmentStatus == ConversationAssignmentStatus.Assigned && !string.IsNullOrEmpty(AssignedAgentId))
        {
            return AssignedAgentId;
        }

        return OwnerType == ConversationOwnerType.Personal && !string.IsNullOrEmpty(OwnerId)
            ? OwnerId
            : null;
    }

    /// <summary>
    /// Gets the key that identifies the customer behind the thread across channels: the linked contact when there
    /// is one, otherwise the channel and address, since an unknown sender can only be recognised by where they
    /// wrote from.
    /// </summary>
    /// <returns>The customer key.</returns>
    public string GetCustomerKey()
        => GetCustomerKey(ContactContentItemId, Channel, ContactAddress);

    /// <summary>
    /// Builds the customer key for a contact, or for an unknown sender on a channel.
    /// </summary>
    /// <param name="contactContentItemId">The linked contact, when known.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="contactAddress">The contact's normalized address on the channel.</param>
    /// <returns>The customer key.</returns>
    public static string GetCustomerKey(string contactContentItemId, string channel, string contactAddress)
        => !string.IsNullOrEmpty(contactContentItemId)
            ? $"contact:{contactContentItemId}"
            : $"address:{channel?.ToUpperInvariant()}:{contactAddress}";
}
