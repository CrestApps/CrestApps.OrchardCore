using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Indexes;

/// <summary>
/// The YesSql index used to query <c>SmsConversation</c> documents: find-or-create by DID + customer, list an
/// agent's or queue's inbox, and filter by read/assignment/status.
/// </summary>
public sealed class SmsConversationIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the DID (service address) the thread runs on.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the customer address (E.164).
    /// </summary>
    public string CustomerAddress { get; set; }

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
}
