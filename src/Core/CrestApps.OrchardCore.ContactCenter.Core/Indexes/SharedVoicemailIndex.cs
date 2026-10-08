using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to list the messages in queue shared voicemail boxes.
/// </summary>
public sealed class SharedVoicemailIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the interaction the caller left the message on.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the queue whose shared box holds the message.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets where the message is in its handling.
    /// </summary>
    public SharedVoicemailStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of whoever is handling the message.
    /// </summary>
    public string ClaimedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the caller was sent to voicemail.
    /// </summary>
    public DateTime ReceivedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the message was marked as dealt with, which is what retention ages it by.
    /// </summary>
    public DateTime? ResolvedUtc { get; set; }
}
