using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Models;

/// <summary>
/// The central new entity of the SMS portal: a two-way SMS thread between one of our numbers
/// (<see cref="ServiceAddress"/>) and a customer (<see cref="CustomerAddress"/>). Stored as its own document
/// via <c>ICatalog&lt;SmsConversation&gt;</c>. The document holds only the thread rollup; the message bodies
/// live as individual <c>OmnichannelMessage</c> records linked by an indexed <c>ConversationId</c>.
/// </summary>
public sealed class SmsConversation : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the channel. Always <c>"SMS"</c> for the portal.
    /// </summary>
    public string Channel { get; set; } = SmsWorkspaceConstants.Channel;

    /// <summary>
    /// Gets or sets the DID we own that this thread runs on (the routing key).
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the customer's number (E.164).
    /// </summary>
    public string CustomerAddress { get; set; }

    /// <summary>
    /// Gets or sets who owns the thread: a personal agent inbox or a queue (department).
    /// </summary>
    public SmsConversationOwnerType OwnerType { get; set; } = SmsConversationOwnerType.Personal;

    /// <summary>
    /// Gets or sets the owner identifier: an agent profile id for <see cref="SmsConversationOwnerType.Personal"/>,
    /// or an <c>ActivityQueue</c> id for <see cref="SmsConversationOwnerType.Queue"/>.
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
    public SmsConversationAssignmentStatus AssignmentStatus { get; set; } = SmsConversationAssignmentStatus.Unassigned;

    /// <summary>
    /// Gets or sets the lifecycle status of the thread.
    /// </summary>
    public SmsConversationStatus Status { get; set; } = SmsConversationStatus.Open;

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
    /// Gets or sets the identifiers of the labels applied to the thread.
    /// </summary>
    public IList<string> LabelIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the provider messaging/session window expires, used to warn before a
    /// session-expired send.
    /// </summary>
    public DateTime? WindowExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the conversation was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the conversation was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
