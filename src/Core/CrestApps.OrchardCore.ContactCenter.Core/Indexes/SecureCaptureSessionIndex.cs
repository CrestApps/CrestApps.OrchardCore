using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query secure capture sessions.
/// </summary>
public sealed class SecureCaptureSessionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction the capture is attached to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent that started the capture.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the lifecycle state of the capture.
    /// </summary>
    public SecureCaptureState State { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the capture engaged a secure recording pause that must be resumed
    /// when the capture settles.
    /// </summary>
    public bool EngagedRecordingPause { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a recording pause the capture engaged has been resumed.
    /// </summary>
    public bool RecordingResumed { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the one-time access token that authorizes the customer page.
    /// </summary>
    public string AccessTokenHash { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the capture window expires.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the capture was last modified, which is what retention ages a terminal capture by.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
