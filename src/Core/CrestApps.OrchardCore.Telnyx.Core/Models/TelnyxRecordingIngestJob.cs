namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// Durable, per-tenant record that tracks the secure ingestion of one completed Telnyx recording into the
/// encrypted media store. Persisted in the tenant's own YesSql store, the job is the single source of truth for
/// ingest progress, so a transient download or upload failure is retried with back-off and eventually
/// dead-lettered rather than silently losing the recording.
/// </summary>
public sealed class TelnyxRecordingIngestJob
{
    /// <summary>
    /// Gets or sets the YesSql document identifier.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the interaction the recording belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx recording identifier. It uniquely identifies the recording (and the job), doubles
    /// as the media-store storage key, and is used to fetch the current download URL from Telnyx at ingest time,
    /// so enqueueing is idempotent per recording.
    /// </summary>
    public string RecordingId { get; set; }

    /// <summary>
    /// Gets or sets the media format the recording is stored in (for example, <c>mp3</c>).
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the dispatch state of the job.
    /// </summary>
    public TelnyxRecordingIngestJobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of ingest attempts made so far.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the next ingest attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the opaque media store reference assigned once the recording has been ingested.
    /// </summary>
    public string MediaReference { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the recording bytes have already been persisted, encrypted, into
    /// the media store. Once set, a retry only needs to complete source cleanup and never re-downloads or
    /// re-stores the recording.
    /// </summary>
    public bool MediaStored { get; set; }

    /// <summary>
    /// Gets or sets the message from the last failed ingest attempt.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the job was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the job was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
