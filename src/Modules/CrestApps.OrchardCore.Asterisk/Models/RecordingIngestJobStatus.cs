namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents the dispatch state of a durable recording ingest job.
/// </summary>
public enum RecordingIngestJobStatus
{
    /// <summary>
    /// The recording has not yet been ingested into the durable media store; the background loop will attempt
    /// (or retry) the download-and-store when the job becomes due.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The recording has been successfully downloaded from Asterisk and stored, encrypted, in the media store.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// The recording could not be ingested after exhausting the retry budget and has been parked for manual
    /// inspection rather than retried indefinitely.
    /// </summary>
    DeadLettered = 2,

    /// <summary>
    /// Ingest was intentionally abandoned because the interaction's recording had already been erased (or the
    /// interaction no longer exists), so any media written for it was cleaned up and the job was not retried.
    /// </summary>
    Cancelled = 3,
}
