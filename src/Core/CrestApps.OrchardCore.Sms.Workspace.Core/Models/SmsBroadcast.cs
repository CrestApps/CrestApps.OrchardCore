using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;

namespace CrestApps.OrchardCore.Sms.Workspace.Core.Models;

/// <summary>
/// A one-composer, many-recipient SMS send. The <b>broadcast</b> flavor fans out to individual 1:1 threads
/// (one conversation per recipient, no cross-visibility) — the compliant default. A durable background task
/// works the recipient list so a large send survives a restart and never double-sends a processed recipient.
/// </summary>
public sealed class SmsBroadcast : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the broadcast name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the sending number (our DID, E.164) every message goes out from.
    /// </summary>
    public string FromNumber { get; set; }

    /// <summary>
    /// Gets or sets the message body sent to every recipient.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the recipient numbers (E.164).
    /// </summary>
    public IList<string> Recipients { get; set; } = [];

    /// <summary>
    /// Gets or sets the recipient numbers already processed, so a resumed sweep never re-sends them.
    /// </summary>
    public IList<string> ProcessedRecipients { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the agent who owns the broadcast; each fanned-out 1:1 thread is assigned
    /// to them.
    /// </summary>
    public string OwnerAgentId { get; set; }

    /// <summary>
    /// Gets or sets the broadcast lifecycle status.
    /// </summary>
    public SmsBroadcastStatus Status { get; set; } = SmsBroadcastStatus.Draft;

    /// <summary>
    /// Gets or sets the number of recipients sent successfully.
    /// </summary>
    public int SentCount { get; set; }

    /// <summary>
    /// Gets or sets the number of recipients that failed to send.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the broadcast was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the broadcast finished processing.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the broadcast was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
