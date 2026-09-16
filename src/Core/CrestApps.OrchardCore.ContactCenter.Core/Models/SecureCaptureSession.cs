using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents an agent-assisted secure capture session: a bounded window during which a customer submits
/// sensitive data on a dedicated secure page instead of speaking it to the agent. The session never holds a raw
/// sensitive value. It retains only the masked representation and the durable token reference the tokenization
/// sink returns, so the agent, the supervisor, and the recording never see the data the customer entered.
/// </summary>
public sealed class SecureCaptureSession : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the interaction the capture is attached to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the Orchard user identifier of the agent that started the capture.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the sensitive field kinds the customer is asked to provide.
    /// </summary>
    public IList<SecureCaptureField> RequestedFields { get; set; } = [];

    /// <summary>
    /// Gets or sets the lifecycle state of the capture.
    /// </summary>
    public SecureCaptureState State { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hash of the one-time access token that authorizes the customer page. The raw
    /// token is returned only once, when the capture is created, and is never persisted.
    /// </summary>
    public string AccessTokenHash { get; set; }

    /// <summary>
    /// Gets or sets the masked representations retained for the audit trail, keyed by the field kind. A masked
    /// value is safe to show the agent, such as the last four digits of a card number.
    /// </summary>
    public IDictionary<SecureCaptureField, string> MaskedValues { get; set; } = new Dictionary<SecureCaptureField, string>();

    /// <summary>
    /// Gets or sets the durable token references the tokenization sink returned, keyed by the field kind. A token
    /// lets an authorized downstream system act on the value without the platform ever storing it.
    /// </summary>
    public IDictionary<SecureCaptureField, string> TokenReferences { get; set; } = new Dictionary<SecureCaptureField, string>();

    /// <summary>
    /// Gets or sets a value indicating whether the capture engaged a secure recording pause that must be resumed
    /// when the capture settles.
    /// </summary>
    public bool EngagedRecordingPause { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a recording pause the capture engaged has been resumed. It stays
    /// <see langword="false"/> until the resume is confirmed, so a settled capture whose resume failed can be
    /// found and retried rather than leaving recording suppressed indefinitely.
    /// </summary>
    public bool RecordingResumed { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the capture was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the capture window expires. After this instant the capture can no longer be
    /// completed and is eligible to be expired.
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the customer completed the capture, when it reached the completed state.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the capture was cancelled by the agent, when it was cancelled.
    /// </summary>
    public DateTime? CancelledUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the capture was last modified, which is the settlement time a terminal capture
    /// is aged by for retention.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
